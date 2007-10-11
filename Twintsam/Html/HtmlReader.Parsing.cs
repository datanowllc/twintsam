using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Xml;

namespace Twintsam.Html
{
    public partial class HtmlReader
    {
        private CurrentTokenizerTokenState _currentTokenizerTokenState = CurrentTokenizerTokenState.Ignored;

        #region 8.2.4.3.1 The stack of open elements
        // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#stack
        /// <remarks>
        /// To achieve the "stack with random access facilities", we're using a linked list.
        /// The "top of the stack" is the <b>first</b> element in the list; this allows enumeration
        /// from top to bottom using the list enumerator (from first to last).
        /// </remarks>
        private LinkedList<Token> _openElements = new LinkedList<Token>();

        private bool IsInScope(string name, bool inTableScope)
        {
            Debug.Assert(String.Equals(name, name.ToLowerInvariant(), StringComparison.Ordinal));

            foreach (Token openElement in _openElements) {
                if (name == openElement.name) {
                    return true;
                } else if (openElement.name == "table") {
                    return false;
                } else if (!inTableScope && Constants.IsScopingElement(openElement.name)) {
                    // XXX: html is a scoping element so it will be treated differently depending on whether we're inTableScope or not but still return false in both cases
                    return false;
                }
            }
            return false;
        }

        private bool IsInScope(Token token, bool inTableScope)
        {
            Debug.Assert(token != null && token.tokenType == XmlNodeType.Element);

            foreach (Token openElement in _openElements) {
                if (token == openElement) {
                    return true;
                } else if (openElement.name == "table") {
                    return false;
                } else if (!inTableScope && Constants.IsScopingElement(openElement.name)) {
                    // XXX: html is a scoping element so it will be treated differently depending on whether we're inTableScope or not but still return false in both cases
                    return false;
                }
            }
            return false;
        }
        #endregion

        #region 8.2.4.3.2. The list of active formatting elements
        // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#list-of4
        /// <remarks>
        /// Instead of inserting "markers" into the list, we insert a list into a stack of lists.
        /// </remarks>
        private Stack<LinkedList<Token>> _activeFormattingElements = new Stack<LinkedList<Token>>(
            new LinkedList<Token>[] { new LinkedList<Token>() });

        private LinkedListNode<Token> ElementInActiveFormattingElements(string name)
        {
            if (_activeFormattingElements.Count > 0) {
                for (LinkedListNode<Token> element = _activeFormattingElements.Peek().Last;
                    element != null; element = element.Previous) {
                    Debug.Assert(element.Value != null);
                    if (element.Value.name == name) {
                        return element;
                    }
                }
            }
            return null;
        }

        private void ReconstructActiveFormattingElements()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#reconstruct
            if (_activeFormattingElements.Count > 0) {
                // Step 3: Let entry be the last (most recently added) element in the list of active formatting elements.
                LinkedListNode<Token> entry = _activeFormattingElements.Peek().Last;
                Debug.Assert(entry == null || entry.Value != null);
                // Step 1: If there are no entries in the list of active formatting elements, then there is nothing to reconstruct; stop this algorithm.
                // Step 2: If the last (most recently added) entry in the list of active formatting elements is a marker, ...
                // ...or if it is an element that is in the stack of open elements, then there is nothing to reconstruct; stop this algorithm.
                if (entry != null && !_openElements.Contains(entry.Value)) {
                    // Step 4: If there are no entries before entry in the list of active formatting elements, then jump to step 8.
                    while (entry.Previous != null) {
                        // Step 5: Let entry be the entry one earlier than entry in the list of active formatting elements.
                        entry = entry.Previous;
                        // Step 6: If entry is neither a marker nor an element that is also in the stack of open elements, go to step 4.
                        if (_openElements.Contains(entry.Value)) {
                            // Step 7: Let entry be the element one later than entry in the list of active formatting elements.
                            entry = entry.Next;
                            break;
                        }
                    }
                    do {
                        // Step 8: Perform a shallow clone of the element entry to obtain clone.
                        // Step 10: Replace the entry for entry in the list with an entry for clone.
                        entry.Value = entry.Value.Clone();
                        // Step 9: Append clone to the current node and push it onto the stack of open elements  so that it is the new current node.
                        InsertHtmlElement(entry.Value);
                        // Step 7: Let entry be the element one later than entry in the list of active formatting elements.
                        entry = entry.Next;
                    } while (entry != null); // Step 11: If the entry for clone in the list of active formatting elements is not the last entry in the list, return to step 7.
                }
            }
        }

        private void ClearActiveFormattingElements()
        {
            _activeFormattingElements.Pop();
        }
        #endregion

        #region 8.2.4.3.3 Creating and inserting HTML elements
        private CurrentTokenizerTokenState InsertHtmlElement()
        {
            Debug.Assert(_tokenizer.TokenType == XmlNodeType.Element);
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#insert
            _openElements.AddFirst(_tokenizer.Token);
            return CurrentTokenizerTokenState.Emitted;
        }
        private void InsertHtmlElement(Token token)
        {
            Debug.Assert(token != null && token.tokenType == XmlNodeType.Element);
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#insert
            _pendingOutputTokens.Enqueue(token);
            _openElements.AddFirst(token);
        }
        #endregion

        #region 8.2.4.3.4. Closing elements that have implied end tags
        // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#generate
        private bool GenerateImpliedEndTags(string omitted)
        {
            Debug.Assert(omitted == null || String.Equals(omitted, omitted.ToLowerInvariant(), StringComparison.Ordinal));

            if (_openElements.Count > 0) {
                string element = _openElements.First.Value.name;
                if ((omitted == null || element != omitted)
                    && Constants.HasOptionalEndTag(element)) {
                    _tokenizer.PushToken(Token.CreateEndTag(element));
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region 8.2.4.3.6. The insertion mode
        // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#reset
        private void ResetInsertionMode(string contextNodeName)
        {
            // TODO: step 3 (fragment case)
            LinkedListNode<Token> node = _openElements.First;
            if (node == null) {
                throw new InvalidOperationException();
            }
            do {
                string nodeName;
                if (node.Next == null) {
                    Debug.Assert(_fragmentCase);
                    nodeName = contextNodeName;
                } else {
                    nodeName = node.Value.name;
                }
                switch (nodeName) {
                case "select":
                    Debug.Assert(_fragmentCase);
                    _insertionMode = InsertionMode.InSelect;
                    return;
                case "td":
                case "th":
                    _insertionMode = InsertionMode.InCell;
                    return;
                case "tr":
                    _insertionMode = InsertionMode.InRow;
                    return;
                case "tbody":
                case "thead":
                case "tfoot":
                    _insertionMode = InsertionMode.InTableBody;
                    return;
                case "caption":
                    _insertionMode = InsertionMode.InCaption;
                    return;
                case "colgroup":
                    Debug.Assert(_fragmentCase);
                    _insertionMode = InsertionMode.InColumnGroup;
                    return;
                case "table":
                    _insertionMode = InsertionMode.InTable;
                    return;
                case "head":
                    Debug.Assert(_fragmentCase);
                    // "in body"! not "in head"!
                    _insertionMode = InsertionMode.InBody;
                    return;
                case "body":
                    _insertionMode = InsertionMode.InBody;
                    return;
                case "frameset":
                    Debug.Assert(_fragmentCase);
                    _insertionMode = InsertionMode.InFrameset;
                    return;
                case "html":
                    throw new NotImplementedException();
                default:
                    node = node.Next;
                    break;
                }
            } while (node != null);
            
            // Step 15
            Debug.Assert(_fragmentCase);
            _insertionMode = InsertionMode.InBody;
        }
        #endregion

        public override bool Read()
        {
            _attributeIndex = -1;
            _inAttributeValue = false;

            if (NodeType == XmlNodeType.Element && !IsEmptyElement) {
                _depth++;
            }

            if (_pendingOutputTokens.Count > 0) {
                _pendingOutputTokens.Dequeue();
                if (_pendingOutputTokens.Count > 0) {
                    if (NodeType == XmlNodeType.EndElement) {
                        Debug.Assert(_depth > 0);
                        _depth--;
                    }
                    return true;
                }
                else if (_currentTokenizerTokenState == CurrentTokenizerTokenState.Emitted)
                {
                    if (NodeType == XmlNodeType.EndElement) {
                        Debug.Assert(_depth > 0);
                        _depth--;
                    }
                    return !_tokenizer.EOF;
                }
            }
            if (_tokenizer.EOF) {
                return false;
            }

            do {
                if (_currentTokenizerTokenState != CurrentTokenizerTokenState.Unprocessed
                    && !_tokenizer.Read()) {
                    _currentTokenizerTokenState = ProcessEndOfFile();
                } else {
                    _currentTokenizerTokenState = ParseToken();
                }
            } while (_currentTokenizerTokenState != CurrentTokenizerTokenState.Emitted
                    && _pendingOutputTokens.Count == 0);

            if (NodeType == XmlNodeType.EndElement) {
                Debug.Assert(_depth > 0);
                _depth--;
            }

            return _pendingOutputTokens.Count > 0 || !_tokenizer.EOF;
        }

        private CurrentTokenizerTokenState ParseToken()
        {
            switch (_phase) {
            case TreeConstructionPhase.Initial:
                return ParseInitial();
            case TreeConstructionPhase.Root:
                return ParseRoot();
            case TreeConstructionPhase.Main:
                return ParseMain();
            case TreeConstructionPhase.CdataOrRcdata:
                return ParseCdataOrRcdata();
            case TreeConstructionPhase.TrailingEnd:
                return ParseTrailingEnd();
            default:
                throw new InvalidOperationException();
            }
        }

        private string ExtractLeadingWhitespace()
        {
            Debug.Assert(_tokenizer.TokenType == XmlNodeType.Text);
            string value = _tokenizer.Value;
            if (Constants.IsSpaceCharacter(value[0])) {
                string nonWhitespace = _tokenizer.Value.TrimStart(Constants.SpaceCharacters.ToCharArray());
                string whitespace = _tokenizer.Value.Substring(0, _tokenizer.Value.Length - nonWhitespace.Length);
                _tokenizer.ReplaceToken(Token.CreateText(nonWhitespace));
                return whitespace;
            } else {
                return String.Empty;
            }
        }

        private void ActAsIfTokenHadBeenSeen(Token token)
        {
            _tokenizer.PushToken(token);
            CurrentTokenizerTokenState state;
            do {
                state = ParseToken();
            } while (state == CurrentTokenizerTokenState.Unprocessed);
            // This algorithm can't work if processing the token implies processing other tokens as well
            Debug.Assert(_tokenizer.Token == token);
#if DEBUG
            Debug.Assert(_tokenizer.Read());
#else
            _tokenizer.Read();
#endif
            if (state == CurrentTokenizerTokenState.Emitted) {
                _pendingOutputTokens.Enqueue(token);
            }
        }

        private CurrentTokenizerTokenState ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token token)
        {
            _tokenizer.PushToken(token);
            return CurrentTokenizerTokenState.Unprocessed;
        }

        private CurrentTokenizerTokenState ProcessEndOfFile()
        {
            switch (_phase) {
            case TreeConstructionPhase.Initial:
                // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#the-initial0
                OnParseError("Unexpected end of stream. Expected DOCTYPE.");
                _compatMode = CompatibilityMode.QuirksMode;
                _phase = TreeConstructionPhase.Root;
                goto case TreeConstructionPhase.Root;
            case TreeConstructionPhase.Root:
                // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#the-root1
                // XXX: that's not exactly what the spec says, but it has an equivalent result.
                InsertHtmlElement(Token.CreateStartTag("html"));
                _phase = TreeConstructionPhase.Main;
                goto case TreeConstructionPhase.Main;
            case TreeConstructionPhase.CdataOrRcdata:
                // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#generic0
                OnParseError("Unexpected end of stream in CDATA or RCDATA");
                Debug.Assert(Constants.IsCdataElement(_openElements.First.Value.name)
                    || Constants.IsRcdataElement(_openElements.First.Value.name));
                _pendingOutputTokens.Enqueue(Token.CreateEndTag(_openElements.First.Value.name));
                _openElements.RemoveFirst();
                _phase = TreeConstructionPhase.Main;
                goto case TreeConstructionPhase.Main;
            case TreeConstructionPhase.Main:
                // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#the-main0
                if (GenerateImpliedEndTags(null)) {
                    // an implied end tag has been generated (i.e. a token has been pushed to the tokenizer, ready to be processed).
                    return CurrentTokenizerTokenState.Unprocessed;
                }
                // XXX: special case for /head, /body and /frameset
                if (_openElements.Count > 0) {
                    Token currentNode = _openElements.First.Value;
                    if ((_insertionMode == InsertionMode.AfterHead
                            && currentNode.name == "head")
                        || (_insertionMode == InsertionMode.AfterBody
                            && currentNode.name == "body")
                        || (_insertionMode == InsertionMode.AfterFrameset
                            && currentNode.name == "frameset")) {
                        _pendingOutputTokens.Enqueue(Token.CreateEndTag(currentNode.name));
                        _openElements.RemoveFirst();
                    }
                }
                // Back to normal processing
                if (_openElements.Count > 2) {
                    OnParseError("Unexpected end of stream. Missing closing tags.");
                } else if (_openElements.Count == 2
                    && _openElements.First.Value.name != "body"
                    && _openElements.First.Value.name != "head") {
                    OnParseError(
                        String.Concat("Unexpected end of stream. Expected end tag (",
                            _openElements.First.Value.name, ") first."));
                } else if (_fragmentCase && _openElements.Count > 1
                    && _openElements.First.Next.Value.name != "body"
                    && _openElements.First.Next.Value.name != "head") {
                    OnParseError("Unexpected end of stream in HTML fragment.");
                }
                // XXX: imply an empty head such that every produced document has at least a head
                if (_insertionMode == InsertionMode.BeforeHead) {
                    InsertHtmlElement(Token.CreateStartTag("head"));
                } else if (_openElements.Count == 1 && _insertionMode == InsertionMode.AfterHead) {
                    // XXX: imply an empty body such that every produced document has at least a body
                    InsertHtmlElement(Token.CreateStartTag("body"));
                }
                // XXX: remove the "html" root in the "fragment case"
                if (_fragmentCase) {
                    Debug.Assert(_openElements.Last.Value.name == "html");
                    _openElements.RemoveLast();
                }
                // XXX: generate end tags for each open element
                while (_openElements.First != null) {
                    Token token = _openElements.First.Value;
                    _openElements.RemoveFirst();
                    _pendingOutputTokens.Enqueue(Token.CreateEndTag(token.name));
                    // XXX: imply an empty body such that every produced document has at least a body
                    if (token.name == "head") {
                        InsertHtmlElement(Token.CreateStartTag("body"));
                    }
                }
                return CurrentTokenizerTokenState.Emitted;
            case TreeConstructionPhase.TrailingEnd:
                // XXX: special case for /html
                Debug.Assert(_openElements.Count <= 1);
                foreach (Token token in _openElements) {
                    _pendingOutputTokens.Enqueue(Token.CreateEndTag(token.name));
                }
                _openElements.Clear();
                // Back to normal processing: nothing to do
                return CurrentTokenizerTokenState.Emitted;
            default:
                throw new InvalidOperationException();
            }
        }

        private CurrentTokenizerTokenState ParseInitial()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#the-initial0
            switch (_tokenizer.TokenType) {
            case XmlNodeType.Whitespace:
                return CurrentTokenizerTokenState.Ignored;
            case XmlNodeType.Comment:
                return CurrentTokenizerTokenState.Emitted;
            case XmlNodeType.DocumentType:
                if (!String.Equals(_tokenizer.Name, "HTML", StringComparison.OrdinalIgnoreCase)) {
                    OnParseError("DOCTYPE name is not HTML (case-insensitive)");
                    _compatMode = CompatibilityMode.QuirksMode;
                } else if (_tokenizer.AttributeCount != 0) {
                    OnParseError("DOCTYPE has public and/or system identifier");
                    if (_tokenizer.IsIncorrectDoctype) {
                        _compatMode = CompatibilityMode.QuirksMode;
                    }
                    string systemId = _tokenizer.GetAttribute("SYSTEM");
                    if (systemId != null && String.Equals(systemId, QuirksModeDoctypeSystemId, StringComparison.OrdinalIgnoreCase)) {
                        _compatMode = CompatibilityMode.QuirksMode;
                    } else {
                        string publicId = _tokenizer.GetAttribute("PUBLIC");
                        if (publicId != null) {
                            publicId = publicId.ToLowerInvariant();
                            if (Constants.Is(QuirksModeDoctypePublicIds, publicId)) {
                                _compatMode = CompatibilityMode.QuirksMode;
                            } else if (Constants.Is(QuirksModeDoctypePublicIdsWhenSystemIdIsMissing, publicId)) {
                                _compatMode = (systemId == null) ? CompatibilityMode.QuirksMode : CompatibilityMode.AlmostStandards;
                            }
                        }
                    }
                } else if (_tokenizer.IsIncorrectDoctype) {
                    _compatMode = CompatibilityMode.QuirksMode;
                }
                _phase = TreeConstructionPhase.Root;
                return CurrentTokenizerTokenState.Emitted;
            case XmlNodeType.Element:
            case XmlNodeType.EndElement:
            case XmlNodeType.Text:
                // XXX: For text tokens, we should extract and ignore leading whitespace, but this will be done in the root phase.
                OnParseError("Missing DOCTYPE.");
                _compatMode = CompatibilityMode.QuirksMode;
                _phase = TreeConstructionPhase.Root;
                return CurrentTokenizerTokenState.Unprocessed;
            default:
                throw new InvalidOperationException(
                    String.Concat("Unexpected token type: ",
                        Enum.GetName(typeof(XmlNodeType), _tokenizer.TokenType)));
            }
        }

        private CurrentTokenizerTokenState ParseRoot()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#the-root1
            switch (_tokenizer.TokenType) {
            case XmlNodeType.DocumentType:
                OnParseError("Misplaced or duplicate DOCTYPE. Ignored.");
                return CurrentTokenizerTokenState.Ignored;
            case XmlNodeType.Comment:
                return CurrentTokenizerTokenState.Emitted;
            case XmlNodeType.Whitespace:
                return CurrentTokenizerTokenState.Ignored;
            case XmlNodeType.Text:
                _phase = TreeConstructionPhase.Main;
                ExtractLeadingWhitespace(); // ignore returned whitespace, because whitespace is ignore in this phase (see above)
                goto case XmlNodeType.EndElement;
            case XmlNodeType.Element:
                if (String.Equals(_tokenizer.Name, "html", StringComparison.Ordinal)) {
                    _phase = TreeConstructionPhase.Main;
                    // XXX: instead of creating an "html" node and then copying the attributes in the main phase, we just use directly the current "html" start tag token
                    return InsertHtmlElement();
                } else {
                    goto case XmlNodeType.EndElement;
                }
            case XmlNodeType.EndElement:
                InsertHtmlElement(Token.CreateStartTag("html"));
                _phase = TreeConstructionPhase.Main;
                return CurrentTokenizerTokenState.Unprocessed;
            default:
                throw new InvalidOperationException(
                    String.Concat("Unexpected token type: ",
                        Enum.GetName(typeof(XmlNodeType), _tokenizer.TokenType)));
            }
        }

        private CurrentTokenizerTokenState ParseCdataOrRcdata()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#generic0
            Debug.Assert(Constants.IsCdataElement(_openElements.First.Value.name)
                || Constants.IsRcdataElement(_openElements.First.Value.name));

            switch (_tokenizer.TokenType) {
            case XmlNodeType.Whitespace:
            case XmlNodeType.Text:
                return CurrentTokenizerTokenState.Emitted;
            case XmlNodeType.EndElement:
                _phase = TreeConstructionPhase.Main;
                Token currentNode = _openElements.First.Value;
                _openElements.RemoveFirst();
                if (currentNode.name != _tokenizer.Name) {
                    OnParseError(
                        String.Concat("CDATA or RCDATA ends with an end tag with unexpected name: ",
                            _tokenizer.Name, ". Expected: ", currentNode.name, "."));
                    _pendingOutputTokens.Enqueue(Token.CreateEndTag(currentNode.name));
                    return CurrentTokenizerTokenState.Ignored;
                }
                return CurrentTokenizerTokenState.Emitted;
            case XmlNodeType.Comment:
                _phase = TreeConstructionPhase.Main;
                OnParseError("Unexpected comment in CDATA or RCDATA. Expected end tag.");
                _pendingOutputTokens.Enqueue(Token.CreateEndTag(_openElements.First.Value.name));
                return CurrentTokenizerTokenState.Ignored;
            case XmlNodeType.Element:
                _phase = TreeConstructionPhase.Main;
                OnParseError("Unexpected start tag in CDATA or RCDATA. Expected end tag.");
                _pendingOutputTokens.Enqueue(Token.CreateEndTag(_openElements.First.Value.name));
                return CurrentTokenizerTokenState.Ignored;
            default:
                throw new InvalidOperationException(
                    String.Concat("Unexpected token type: ",
                        Enum.GetName(typeof(XmlNodeType), _tokenizer.TokenType)));
            }
        }

        private CurrentTokenizerTokenState ParseMain()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#how-to0
            switch (_tokenizer.TokenType) {
            case XmlNodeType.DocumentType:
                OnParseError("Unexpected DOCTYPE. Ignored");
                return CurrentTokenizerTokenState.Ignored;
            case XmlNodeType.Element:
                // XXX: the html start tag has already been emitted in the root phase.
                if (_tokenizer.Name == "html") {
                    OnParseError("Unexpected start tag: html");
                    if (_tokenizer.HasAttributes) {
                        // XXX: hack to emit the attributes: we fake an <html /> self-closing element.
                        Token htmlToken = _tokenizer.Token;
                        htmlToken.hasTrailingSolidus = true;
                        _pendingOutputTokens.Enqueue(htmlToken);
                        return CurrentTokenizerTokenState.Ignored;
                    }
                }
                goto case XmlNodeType.EndElement;
            case XmlNodeType.EndElement:
            case XmlNodeType.Comment:
            case XmlNodeType.Text:
            case XmlNodeType.Whitespace:
                switch (_insertionMode) {
                case InsertionMode.BeforeHead:
                    return ParseMainBeforeHead();
                case InsertionMode.InHead:
                    return ParseMainInHead();
                case InsertionMode.InHeadNoscript:
                    return ParseMainInHeadNoscript();
                case InsertionMode.AfterHead:
                    return ParseMainAfterHead();
                case InsertionMode.InBody:
                    return ParseMainInBody();
                case InsertionMode.InTable:
                    return ParseMainInTable();
                case InsertionMode.InCaption:
                    return ParseMainInCaption();
                case InsertionMode.InColumnGroup:
                case InsertionMode.InTableBody:
                case InsertionMode.InRow:
                case InsertionMode.InCell:
                case InsertionMode.InSelect:
                    throw new NotImplementedException();
                case InsertionMode.AfterBody:
                    return ParseMainAfterBody();
                case InsertionMode.InFrameset:
                case InsertionMode.AfterFrameset:
                    throw new NotImplementedException();
                default:
                    throw new InvalidOperationException();
                }
            default:
                throw new InvalidOperationException(
                    String.Concat("Unexpected token type: ",
                        Enum.GetName(typeof(XmlNodeType), _tokenizer.TokenType)));
            }
        }

        private CurrentTokenizerTokenState ParseMainBeforeHead()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#before4
            switch (_tokenizer.TokenType) {
            case XmlNodeType.Whitespace:
            case XmlNodeType.Comment:
                return CurrentTokenizerTokenState.Emitted;
            case XmlNodeType.Text:
                string whitespace = ExtractLeadingWhitespace();
                _tokenizer.PushToken(Token.CreateStartTag("head"));
                if (whitespace.Length > 0) {
                    _tokenizer.PushToken(Token.CreateWhitespace(whitespace));
                    goto case XmlNodeType.Whitespace;
                } else {
                    return CurrentTokenizerTokenState.Unprocessed;
                }
            case XmlNodeType.Element:
                if (_tokenizer.Name == "head") {
                    _insertionMode = InsertionMode.InHead;
                    return InsertHtmlElement();
                } else {
                    return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateStartTag("head"));
                }
            case XmlNodeType.EndElement:
                switch (_tokenizer.Name) {
                case "head":
                case "html":
                case "body":
                case "p":
                case "br":
                    return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateStartTag("head"));
                default:
                    OnParseError(String.Concat("Unexpected end tag (", _tokenizer.Name, "). Ignored."));
                    return CurrentTokenizerTokenState.Ignored;
                }
            default:
                throw new InvalidOperationException(
                    String.Concat("Unexpected token type: ",
                        Enum.GetName(typeof(XmlNodeType), _tokenizer.TokenType)));
            }
        }

        private CurrentTokenizerTokenState ParseMainInHead()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#parsing-main-inhead
            switch (_tokenizer.TokenType) {
            case XmlNodeType.Whitespace:
            case XmlNodeType.Comment:
                return CurrentTokenizerTokenState.Emitted;
            case XmlNodeType.Text:
                string whitespace = ExtractLeadingWhitespace();
                if (whitespace.Length > 0) {
                    _tokenizer.PushToken(Token.CreateWhitespace(whitespace));
                    goto case XmlNodeType.Whitespace;
                } else {
                    return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateEndTag("head"));
                }
            case XmlNodeType.Element:
                switch (_tokenizer.Name) {
                case "base":
                case "link":
                    // XXX: we don't bother adding to and then immediately popping from the stack of open elements.
                    return CurrentTokenizerTokenState.Emitted;
                case "meta":
                    // TODO: change charset if needed
                    // XXX: we don't bother adding to and then immediately popping from the stack of open elements.
                    return CurrentTokenizerTokenState.Emitted;
                case "title":
                    _phase = TreeConstructionPhase.CdataOrRcdata;
                    _tokenizer.ContentModel = ContentModel.Rcdata;
                    return InsertHtmlElement();
                case "noscript":
                    // TODO: case when scripting is enabled:
                    //if (_scriptingElabled)
                    //{
                    //    goto case "style";
                    //}
                    _insertionMode = InsertionMode.InHeadNoscript;
                    return InsertHtmlElement();
                case "style":
                    _phase = TreeConstructionPhase.CdataOrRcdata;
                    _tokenizer.ContentModel = ContentModel.Cdata;
                    return InsertHtmlElement();
                case "script":
                    // TODO: script execution
                    _phase = TreeConstructionPhase.CdataOrRcdata;
                    _tokenizer.ContentModel = ContentModel.Cdata;
                    return InsertHtmlElement();
                case "head":
                    OnParseError("Unexpected HEAD start tag. Ignored.");
                    return CurrentTokenizerTokenState.Ignored;
                default:
                    return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateEndTag("head"));
                }
            case XmlNodeType.EndElement:
                switch (_tokenizer.Name) {
                case "head":
                    // XXX: we don't emit the head end tag, we'll emit it later before switching to the "in body" insertion mode
                    _insertionMode = InsertionMode.AfterHead;
                    return CurrentTokenizerTokenState.Ignored;
                case "body":
                case "html":
                case "p":
                case "br":
                    return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateEndTag("head"));
                default:
                    OnParseError(String.Concat("Unexpected end tag (", _tokenizer.Name, "). Ignored."));
                    return CurrentTokenizerTokenState.Ignored;
                }
            default:
                throw new InvalidOperationException(
                    String.Concat("Unexpected token type: ",
                        Enum.GetName(typeof(XmlNodeType), _tokenizer.TokenType)));
            }
        }

        private CurrentTokenizerTokenState ParseMainInHeadNoscript()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#in-head0
            switch (_tokenizer.TokenType) {
            case XmlNodeType.Whitespace:
            case XmlNodeType.Comment:
                return ParseMainInHead();
            case XmlNodeType.Text:
                string whitespace = ExtractLeadingWhitespace();
                if (whitespace.Length > 0) {
                    _tokenizer.PushToken(Token.CreateWhitespace(whitespace));
                    goto case XmlNodeType.Whitespace;
                } else {
                    OnParseError("Unexpected non-whitespace character in noscript in head");
                    return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateEndTag("noscript"));
                }
            case XmlNodeType.Element:
                switch (_tokenizer.Name) {
                case "link":
                case "meta":
                case "style":
                    return ParseMainInHead();
                case "head":
                case "noscript":
                    OnParseError(
                        String.Concat("Unexpected start tag (", _tokenizer.Name,
                            ") in noscript in head."));
                    return CurrentTokenizerTokenState.Ignored;
                default:
                    OnParseError(
                        String.Concat("Unexpected start tag (", _tokenizer.Name,
                            ") in noscript in head"));
                    return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateEndTag("noscript"));
                }
            case XmlNodeType.EndElement:
                switch (_tokenizer.Name) {
                case "noscript":
                    Debug.Assert(_openElements.First.Value.name == "noscript");
                    _openElements.RemoveFirst();
                    Debug.Assert(_openElements.First.Value.name == "head");
                    _insertionMode = InsertionMode.InHead;
                    return CurrentTokenizerTokenState.Emitted;
                case "p":
                case "br":
                    OnParseError(
                        String.Concat("Unexpected end tag (", _tokenizer.Name,
                            ") in noscript in head"));
                    return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateEndTag("noscript"));
                default:
                    OnParseError(
                        String.Concat("Unexpected end tag (",_tokenizer.Name,
                            ") in noscript in head."));
                    return CurrentTokenizerTokenState.Ignored;
                }
            default:
                throw new InvalidOperationException(
                    String.Concat("Unexpected token type: ",
                        Enum.GetName(typeof(XmlNodeType), _tokenizer.TokenType)));
            }
        }

        private CurrentTokenizerTokenState ParseMainAfterHead()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#after3
            switch (_tokenizer.TokenType) {
            case XmlNodeType.Whitespace:
            case XmlNodeType.Comment:
                return CurrentTokenizerTokenState.Emitted;
            case XmlNodeType.Text:
                string whitespace = ExtractLeadingWhitespace();
                if (whitespace.Length > 0) {
                    _tokenizer.PushToken(Token.CreateWhitespace(whitespace));
                    goto case XmlNodeType.Whitespace;
                } else {
                    return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateStartTag("body"));
                }
            case XmlNodeType.Element:
                switch (_tokenizer.Name) {
                case "body":
                    // XXX: that's where we emit the head end tag
                    Debug.Assert(_openElements.First.Value.name == "head");
                    _openElements.RemoveFirst();
                    _pendingOutputTokens.Enqueue(Token.CreateEndTag("head"));
                    _insertionMode = InsertionMode.InBody;
                    return InsertHtmlElement();
                case "frameset":
                    // XXX: that's where we emit the head end tag
                    Debug.Assert(_openElements.First.Value.name == "head");
                    _openElements.RemoveFirst();
                    _pendingOutputTokens.Enqueue(Token.CreateEndTag("head"));
                    _insertionMode = InsertionMode.InFrameset;
                    return InsertHtmlElement();
                case "base":
                case "link":
                case "meta":
                case "script":
                case "style":
                case "title":
                    OnParseError(String.Concat("Unexpected start tag (", _tokenizer.Name, "). Should be in head."));
                    return ParseMainInHead();
                default:
                    return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateStartTag("body"));
                }
            case XmlNodeType.EndElement:
                return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateStartTag("body"));
            default:
                throw new InvalidOperationException(
                    String.Concat("Unexpected token type: ",
                        Enum.GetName(typeof(XmlNodeType), _tokenizer.TokenType)));
            }
        }

        private CurrentTokenizerTokenState ParseMainInBody()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#in-body
            switch (_tokenizer.TokenType) {
            case XmlNodeType.Whitespace:
            case XmlNodeType.Text:
                ReconstructActiveFormattingElements();
                return CurrentTokenizerTokenState.Emitted;
            case XmlNodeType.Comment:
                return CurrentTokenizerTokenState.Emitted;
            case XmlNodeType.Element:
                switch (_tokenizer.Name) {
                case "base":
                case "link":
                case "meta":
                case "script":
                case "style":
                    return ParseMainInHead();
                case "title":
                    OnParseError("Unexpected title start tag in body.");
                    return ParseMainInHead();
                case "body":
                    if (_fragmentCase
                        && ((_openElements.Count > 1 && _openElements.First.Next.Value.name != "body")
                            || _openElements.Count == 1)) {
                        return CurrentTokenizerTokenState.Ignored;
                    } else if (_tokenizer.HasAttributes) {
                        OnParseError("Unexpected body start tag in body. NOT ignored because of attributes.");
                        // XXX: use a fake <body /> to "emit" attributes
                        Token bodyToken = _tokenizer.Token;
                        bodyToken.hasTrailingSolidus = true;
                        _pendingOutputTokens.Enqueue(bodyToken);
                        return CurrentTokenizerTokenState.Ignored;
                    } else {
                        OnParseError("Unexpected body start tag in body. Ignored (no attribute).");
                        return CurrentTokenizerTokenState.Ignored;
                    }
                case "address":
                case "blockquote":
                case "center":
                case "dir":
                case "div":
                case "dl":
                case "fieldset":
                case "listing":
                case "menu":
                case "ol":
                case "p":
                case "ul":
                    if (IsInScope("p", false)) {
                        // XXX: that's not what the spec says but it has the same result
                        return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateEndTag("p"));
                    } else {
                        return InsertHtmlElement();
                    }
                case "pre":
                    // FIXME: don't micromanage "<pre>\n", treat as a above for now (don't now it this actually needs fixing or if it'll be handle at the "real tree construction stage")
                    goto case "p";
                case "form":
                    // XXX: no "form element pointer", so no ParseError (will be handle at the "real tree construction stage")
                    goto case "p";
                case "li":
                    if (IsInScope("p", false)) {
                        // XXX: that's not what the spec says but it has the same result
                        return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateEndTag("p"));
                    } else {
                        foreach (Token token in _openElements) {
                            if (token.name == "li") {
                                List<string> poppeds = new List<string>();
                                Token popped;
                                do {
                                    popped = _openElements.First.Value;
                                    _openElements.RemoveFirst();
                                    poppeds.Add(popped.name);
                                    _pendingOutputTokens.Enqueue(Token.CreateEndTag(popped.name));
                                } while (popped.name != "li");
                                if (poppeds.Count > 1) {
                                    poppeds.RemoveAt(poppeds.Count - 1);
                                    OnParseError(String.Concat("Missing end tag(s): ",
                                        String.Join(", ", poppeds.ToArray()), "."));
                                }
                                break;
                            } else if ((Constants.IsScopingElement(token.name) || Constants.IsSpecialElement(token.name))
                                && (token.name != "address" && token.name != "div")) {
                                break;
                            }
                        }
                        return InsertHtmlElement();
                    }
                case "dd":
                case "dt":
                    if (IsInScope("p", false)) {
                        // XXX: that's not what the spec says but it has the same result
                        return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateEndTag("p"));
                    } else {
                        foreach (Token token in _openElements) {
                            if (token.name == "dd" || token.name == "dt") {
                                List<string> poppeds = new List<string>();
                                Token popped;
                                do {
                                    popped = _openElements.First.Value;
                                    _openElements.RemoveFirst();
                                    poppeds.Add(popped.name);
                                    _pendingOutputTokens.Enqueue(Token.CreateEndTag(popped.name));
                                } while (popped.name != "dd" && token.name != "dt");
                                if (poppeds.Count > 1) {
                                    poppeds.RemoveAt(poppeds.Count - 1);
                                    OnParseError(String.Concat("Missing end tag(s): ",
                                        String.Join(", ", poppeds.ToArray()), "."));
                                }
                                break;
                            } else if ((Constants.IsScopingElement(token.name) || Constants.IsSpecialElement(token.name))
                                && (token.name != "address" && token.name != "div")) {
                                break;
                            }
                        }
                        return InsertHtmlElement();
                    }
                case "plaintext":
                    if (IsInScope("p", false)) {
                        // XXX: that's not what the spec says but it has the same result
                        return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateEndTag("p"));
                    } else {
                        _tokenizer.ContentModel = ContentModel.PlainText;
                        return InsertHtmlElement();
                    }
                case "h1":
                case "h2":
                case "h3":
                case "h4":
                case "h5":
                case "h6":
                    goto case "p";
                case "a":
                    LinkedListNode<Token> a = ElementInActiveFormattingElements("a");
                    if (a != null) {
                        OnParseError("Unexpected start tag (a) implies end tag (a)");
                        ActAsIfTokenHadBeenSeen(Token.CreateEndTag("a"));
                        _openElements.Remove(a.Value);
                        if (a.List != null) {
                            a.List.Remove(a);
                        }
                    }
                    ReconstructActiveFormattingElements();
                    _activeFormattingElements.Peek().AddLast(_tokenizer.Token);
                    return InsertHtmlElement();
                case "b":
                case "big":
                case "em":
                case "font":
                case "i":
                case "s":
                case "small":
                case "strike":
                case "strong":
                case "tt":
                case "u":
                    ReconstructActiveFormattingElements();
                    _activeFormattingElements.Peek().AddLast(_tokenizer.Token);
                    return InsertHtmlElement();
                case "nobr":
                    ReconstructActiveFormattingElements();
                    if (ElementInActiveFormattingElements("nobr") != null) {
                        OnParseError("Unexpected start tag (nobr) implies end tag (nobr)");
                        return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateEndTag("nobr"));
                    }
                    _activeFormattingElements.Peek().AddLast(_tokenizer.Token);
                    return InsertHtmlElement();
                case "button":
                    if (IsInScope("button", false)) {
                        OnParseError("Unexpected end tag (button) implies end tag (button)");
                        return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateEndTag("button"));
                    }
                    ReconstructActiveFormattingElements();
                    _activeFormattingElements.Push(new LinkedList<Token>());
                    return InsertHtmlElement();
                case "marquee":
                case "object":
                    ReconstructActiveFormattingElements();
                    _activeFormattingElements.Push(new LinkedList<Token>());
                    return InsertHtmlElement();
                case "xmp":
                    ReconstructActiveFormattingElements();
                    _phase = TreeConstructionPhase.CdataOrRcdata;
                    _tokenizer.ContentModel = ContentModel.Cdata;
                    return InsertHtmlElement();
                case "table":
                    if (IsInScope("p", false)) {
                        // XXX: that's not what the spec says but it has the same result
                        return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateEndTag("p"));
                    } else {
                        _insertionMode = InsertionMode.InTable;
                        return InsertHtmlElement();
                    }
                case "area":
                case "basefont":
                case "bgsound":
                case "br":
                case "embed":
                case "img":
                case "param":
                case "spacer":
                case "wbr":
                    ReconstructActiveFormattingElements();
                    // XXX: we don't bother adding to and then immediately popping from the stack of open elements.
                    return CurrentTokenizerTokenState.Emitted;
                case "hr":
                    if (IsInScope("p", false)) {
                        // XXX: that's not what the spec says but it has the same result
                        return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateEndTag("p"));
                    } else {
                        // XXX: we don't bother adding to and then immediately popping from the stack of open elements.
                        return CurrentTokenizerTokenState.Emitted;
                    }
                case "image":
                    OnParseError("Unexpected start tag (image). Treated as img.");
                    Token img = _tokenizer.Token;
                    img.name = "img";
                    _tokenizer.ReplaceToken(img);
                    return CurrentTokenizerTokenState.Unprocessed;
                case "input":
                    ReconstructActiveFormattingElements();
                    InsertHtmlElement();
                    _openElements.RemoveFirst();
                    return CurrentTokenizerTokenState.Emitted;
                case "isindex":
                    throw new NotImplementedException();
                case "textarea":
                    _phase = TreeConstructionPhase.CdataOrRcdata;
                    _tokenizer.ContentModel = ContentModel.Rcdata;
                    return InsertHtmlElement();
                case "iframe":
                case "noembed":
                case "noframe":
                    _phase = TreeConstructionPhase.CdataOrRcdata;
                    _tokenizer.ContentModel = ContentModel.Cdata;
                    return InsertHtmlElement();
                case "noscript":
                    // TODO: case when scripting is enabled:
                    //_phase = TreeConstructionPhase.CdataOrRcdata;
                    //_tokenizer.ContentModel = ContentModel.Cdata;
                    //return CurrentTokenizerTokenState.Emitted;
                    throw new NotImplementedException();
                case "select":
                    ReconstructActiveFormattingElements();
                    _insertionMode = InsertionMode.InSelect;
                    return InsertHtmlElement();
                case "caption":
                case "col":
                case "colgroup":
                case "frame":
                case "frameset":
                case "head":
                case "option":
                case "optgroup":
                case "tbody":
                case "td":
                case "tfoot":
                case "th":
                case "thead":
                case "tr":
                    OnParseError(String.Concat("Unexpected start tag (", _tokenizer.Name, "). Ignored."));
                    return CurrentTokenizerTokenState.Ignored;
                case "event-source":
                case "section":
                case "nav":
                case "article":
                case "aside":
                case "header":
                case "footer":
                case "datagrid":
                case "command":
                    Trace.TraceWarning("Behavior not even yet defined in the current draft for start tag: {0}.", _tokenizer.Name);
                    return InsertHtmlElement();
                default:
                    ReconstructActiveFormattingElements();
                    return InsertHtmlElement();
                }
            case XmlNodeType.EndElement:
                switch (_tokenizer.Name) {
                case "body":
                    if (_fragmentCase
                        && (_openElements.Count > 1 && _openElements.First.Next.Value.name != "body")) {
                        // XXX: this is duplicated in the case "html" below.
                        OnParseError("???");
                        return CurrentTokenizerTokenState.Ignored;
                    }
                    foreach (Token token in _openElements) {
                        switch (token.name) {
                        case "dd":
                        case "dt":
                        case "li":
                        case "p":
                        case "tbody":
                        case "td":
                        case "tfoot":
                        case "th":
                        case "thead":
                        case "tr":
                        case "body":
                        case "html":
                            continue;
                        default:
                            OnParseError("XXX: Unexpected token in the stack of open elements");
                            break;
                        }
                        break;
                    }
                    _insertionMode = InsertionMode.AfterBody;
                    // XXX: similarly to the head end tag, we'll emit the body end tag while emitting the html end tag (i.e. at EOF)
                    return CurrentTokenizerTokenState.Ignored;
                case "html":
                    if (_fragmentCase
                        && (_openElements.Count > 1 && _openElements.First.Next.Value.name != "body")) {
                        // XXX: this is the same test as in the case "body" above, so we act as if an end tag with tag name "body" had been seen: parse error and ignore.
                        OnParseError("???");
                        return CurrentTokenizerTokenState.Ignored;
                    }
                    return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateEndTag("body"));
                case "address":
                case "blockquote":
                case "center":
                case "dir":
                case "div":
                case "dl":
                case "fieldset":
                case "listing":
                case "menu":
                case "ol":
                case "pre":
                case "ul":
                    if (IsInScope(_tokenizer.Name, false) && GenerateImpliedEndTags(null)) {
                        return CurrentTokenizerTokenState.Unprocessed;
                    }
                    if (_openElements.First.Value.name != _tokenizer.Name) {
                        OnParseError(
                            String.Concat("End tag (", _tokenizer.Name,
                                ") seen too early. Expected other end tag (",
                                _openElements.First.Value.name, ")."));
                    }
                    if (IsInScope(_tokenizer.Name, false)) {
                        Token token = _openElements.First.Value;
                        _openElements.RemoveFirst();
                        while (token.name != _tokenizer.Name) {
                            _pendingOutputTokens.Enqueue(Token.CreateEndTag(token.name));
                            token = _openElements.First.Value;
                            _openElements.RemoveFirst();
                        }
                    }
                    return CurrentTokenizerTokenState.Emitted;
                case "form":
                    if (IsInScope("form", false) && GenerateImpliedEndTags(null)) {
                        return CurrentTokenizerTokenState.Unprocessed;
                    }
                    if (_openElements.First.Value.name != "form") {
                        OnParseError(
                            String.Concat("End tag (", "form",
                                ") seen too early. Expected other end tag (",
                                _openElements.First.Value.name, ")."));
                    } else {
                        _openElements.RemoveFirst();
                    }
                    return CurrentTokenizerTokenState.Emitted;
                case "p":
                    if (IsInScope("p", false) && GenerateImpliedEndTags("p")) {
                        return CurrentTokenizerTokenState.Unprocessed;
                    }
                    if (_openElements.First.Value.name != "p") {
                        OnParseError(
                            String.Concat("End tag (", "p",
                                ") seen too early. Expected other end tag (",
                                _openElements.First.Value.name, ")."));
                    }
                    if (IsInScope("p", false)) {
                        do {
                            Token currentNode = _openElements.First.Value;
                            _openElements.RemoveFirst();
                            if (currentNode.name != "p") {
                                _pendingOutputTokens.Enqueue(Token.CreateEndTag(currentNode.name));
                            }
                        } while (IsInScope("p", false));
                        return CurrentTokenizerTokenState.Emitted;
                    } else {
                        return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateStartTag("p"));
                    }
                case "dd":
                case "dt":
                case "li":
                    if (IsInScope(_tokenizer.Name, false) && GenerateImpliedEndTags(_tokenizer.Name)) {
                        return CurrentTokenizerTokenState.Unprocessed;
                    }
                    if (_openElements.First.Value.name != _tokenizer.Name) {
                        OnParseError(
                            String.Concat("End tag (", _tokenizer.Name,
                                ") seen too early. Expected other end tag (",
                                _openElements.First.Value.name, ")."));
                    }
                    if (IsInScope(_tokenizer.Name, false)) {
                        Token token = _openElements.First.Value;
                        _openElements.RemoveFirst();
                        while (token.name != _tokenizer.Name) {
                            _pendingOutputTokens.Enqueue(Token.CreateEndTag(token.name));
                            token = _openElements.First.Value;
                            _openElements.RemoveFirst();
                        }
                    }
                    return CurrentTokenizerTokenState.Emitted;
                case "h1":
                case "h2":
                case "h3":
                case "h4":
                case "h5":
                case "h6":
                    if ((IsInScope("h1", false) || IsInScope("h2", false) || IsInScope("h3", false)
                         || IsInScope("h4", false) || IsInScope("h5", false) || IsInScope("h6", false))
                        && GenerateImpliedEndTags(null)) {
                        return CurrentTokenizerTokenState.Unprocessed;
                    }
                    if (_openElements.First.Value.name != _tokenizer.Name) {
                        OnParseError(
                            String.Concat("End tag (", _tokenizer.Name,
                                ") seen too early. Expected other end tag (",
                                _openElements.First.Value.name, ")."));
                    }
                    if (IsInScope("h1", false) || IsInScope("h2", false) || IsInScope("h3", false)
                         || IsInScope("h4", false) || IsInScope("h5", false) || IsInScope("h6", false)) {
                        Token token = _openElements.First.Value;
                        _openElements.RemoveFirst();
                        while (token.name != "h1" && token.name != "h2" && token.name != "h3"
                               && token.name != "h4" && token.name != "h5" && token.name != "h6") {
                            _pendingOutputTokens.Enqueue(Token.CreateEndTag(token.name));
                            token = _openElements.First.Value;
                            _openElements.RemoveFirst();
                        }
                    }
                    // XXX: isn't the spec implicitly saying we should emit a token with the same name as the last popped node above?
                    return CurrentTokenizerTokenState.Emitted;
                case "a":
                case "b":
                case "big":
                case "em":
                case "font":
                case "i":
                case "nobr":
                case "s":
                case "small":
                case "strike":
                case "strong":
                case "tt":
                case "u":
                    do {
                        // Step 1
                        LinkedListNode<Token> formattingElement = ElementInActiveFormattingElements(_tokenizer.Name);
                        if (formattingElement == null
                            || (_openElements.Contains(formattingElement.Value) && !IsInScope(formattingElement.Value, false))) {
                            OnParseError(String.Concat("No matching start tag (", _tokenizer.Name, ")"));
                            return CurrentTokenizerTokenState.Ignored;
                        }
                        if (!_openElements.Contains(formattingElement.Value)) {
                            OnParseError(String.Concat("No matching start tag (", _tokenizer.Name, ")"));
                            formattingElement.List.Remove(formattingElement);
                            return CurrentTokenizerTokenState.Emitted;
                        }
                        Debug.Assert(_openElements.Contains(formattingElement.Value) && IsInScope(formattingElement.Value, false));
                        if (_openElements.First.Value != formattingElement.Value) {
                            OnParseError("???");
                        }
                        // Step 4
                        LinkedListNode<Token> commonAncestor = _openElements.Find(formattingElement.Value).Previous;
                        // Step 2
                        LinkedListNode<Token> furthestBlock = commonAncestor;
                        while (furthestBlock != null
                            // neither formatting nor phrasing means either scoping or special
                            && !Constants.IsScopingElement(furthestBlock.Value.name)
                            && !Constants.IsSpecialElement(furthestBlock.Value.name)) {
                            furthestBlock = furthestBlock.Previous;
                        }
                        // Step 3
                        if (furthestBlock == null) {
                            formattingElement.List.Remove(formattingElement);
                            Token currentNode = _openElements.First.Value;
                            _openElements.RemoveFirst();
                            while (currentNode != formattingElement.Value) {
                                _pendingOutputTokens.Enqueue(Token.CreateEndTag(currentNode.name));
                                currentNode = _openElements.First.Value;
                                _openElements.RemoveFirst();
                            }
                            return CurrentTokenizerTokenState.Emitted;
                        }
                        // Step 5
                        if (furthestBlock.Previous != null) {
                            // TODO: remove the furthest block from its parent node
                            throw new NotImplementedException();
                        }
                        // Step 6
                        LinkedListNode<Token> bookmark = formattingElement;
                        // Step 7 (Substeps 7.1 and 7.8)
                        for (LinkedListNode<Token> node = furthestBlock.Next, lastNode = furthestBlock;
                            // Substep 7.3
                            node.Value == formattingElement.Value;
                            // Substeps 7.7 and 7.1
                            lastNode = node, node = node.Next) {
                            // Substep 7.2
                            if (!formattingElement.List.Contains(node.Value)) {
                                _pendingOutputTokens.Enqueue(Token.CreateEndTag(node.Value.name));
                                _openElements.Remove(node);
                            } else {
                                // Substep 7.4
                                if (lastNode == furthestBlock) {
                                    bookmark = _activeFormattingElements.Peek().Find(node.Value);
                                }
                                // Substep 7.5: ignore, no need to clone, we're not building a tree here.
                                // TODO: Substep 7.6 (involves reparenting: will be done in the HtmlDocumentBuilder)
                            }
                        }
                        // TODO: Step 8 (involve reparenting: will be done in the HtmlDocumentBuilder)
                        // Step 9
                        _pendingOutputTokens.Enqueue(formattingElement.Value);
                        // TODO: Steps 9, 10 and 11 (involve reparenting: will be done in the HtmlDocumentBuilder)
                        // Step 12
                        if (bookmark != formattingElement) {
                            formattingElement.List.Remove(formattingElement);
                            bookmark.List.AddAfter(bookmark, formattingElement);
                        }
                        // Step 13
                        _openElements.Remove(formattingElement.Value);
                        _openElements.AddBefore(furthestBlock, formattingElement.Value);
                    } while (true); // Step 14
                case "button":
                case "marquee":
                case "object":
                    if (IsInScope(_tokenizer.Name, false)) {
                        if (GenerateImpliedEndTags(null)) {
                            return CurrentTokenizerTokenState.Unprocessed;
                        }
                    }
                    if (_openElements.First.Value.name != _tokenizer.Name) {
                        OnParseError(
                            String.Concat("End tag (", _tokenizer.Name,
                                ") seen too early. Expected other end tag (",
                                _openElements.First.Value.name, ")."));
                    }
                    if (IsInScope(_tokenizer.Name, false)) {
                        Token token = _openElements.First.Value;
                        _openElements.RemoveFirst();
                        while (token.name != _tokenizer.Name) {
                            _pendingOutputTokens.Enqueue(Token.CreateEndTag(token.name));
                            token = _openElements.First.Value;
                            _openElements.RemoveFirst();
                        }
                        ClearActiveFormattingElements();
                    }
                    return CurrentTokenizerTokenState.Emitted;
                case "caption":
                case "col":
                case "colgroup":
                case "frame":
                case "frameset":
                case "head":
                case "option":
                case "optgroup":
                case "tbody":
                case "td":
                case "tfoot":
                case "th":
                case "thead":
                case "tr":
                case "area":
                case "basefont":
                case "bgsound":
                case "br":
                case "embed":
                case "hr":
                case "iframe":
                case "image":
                case "img":
                case "input":
                case "isindex":
                case "noembed":
                case "noframes":
                case "param":
                case "select":
                case "spacer":
                case "table":
                case "textarea":
                case "wbr":
                    OnParseError("???");
                    return CurrentTokenizerTokenState.Ignored;
                case "noscript":
                    // TODO: case when scripting is enabled:
                    //OnParseError("???");
                    //return CurrentTokenizerTokenState.Ignored;
                    throw new NotImplementedException();
                case "event-source":
                case "section":
                case "nav":
                case "article":
                case "aside":
                case "header":
                case "footer":
                case "datagrid":
                case "command":
                    Trace.TraceWarning("Behavior not even yet defined in the current draft for start tag: {0}.", _tokenizer.Name);
                    return CurrentTokenizerTokenState.Emitted;
                default:
                    throw new NotImplementedException();
                }
            default:
                throw new InvalidOperationException(
                    String.Concat("Unexpected token type: ",
                        Enum.GetName(typeof(XmlNodeType), _tokenizer.TokenType)));
            }
        }

        private void ClearStackBackToTableContext()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#clear1
            bool parseError = false;
            while (_openElements.First.Value.name != "table" && _openElements.First.Value.name != "html") {
                parseError = true;
                _openElements.RemoveFirst();
            }
            Debug.Assert(_openElements.First.Value.name != "html" || _fragmentCase);
            if (parseError) {
                OnParseError("??? found open element(s) while clearing the stack back to a table context.");
            }
        }

        private CurrentTokenizerTokenState ParseMainInTable()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#in-table
            switch (_tokenizer.TokenType) {
            case XmlNodeType.Whitespace:
            case XmlNodeType.Comment:
                return CurrentTokenizerTokenState.Emitted;
            case XmlNodeType.Text:
                string whitespace = ExtractLeadingWhitespace();
                if (whitespace.Length > 0) {
                    _tokenizer.PushToken(Token.CreateWhitespace(whitespace));
                    goto case XmlNodeType.Whitespace;
                } else {
                    OnParseError("???");
                    // XXX: the "in table" exception will be handled in the "real tree builder algorithm"
                    return ParseMainInBody();
                }
            case XmlNodeType.Element:
                switch (_tokenizer.Name) {
                case "caption":
                    ClearStackBackToTableContext();
                    _activeFormattingElements.Push(new LinkedList<Token>());
                    _insertionMode = InsertionMode.InCaption;
                    return InsertHtmlElement();
                case "colgroup":
                    ClearStackBackToTableContext();
                    _insertionMode = InsertionMode.InColumnGroup;
                    return InsertHtmlElement();
                case "col":
                    return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateStartTag("colgroup"));
                case "tbody":
                case "tfoot":
                case "thead":
                    ClearStackBackToTableContext();
                    _insertionMode = InsertionMode.InTableBody;
                    return InsertHtmlElement();
                case "td":
                case "th":
                case "tr":
                    return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateStartTag("tbody"));
                case "table":
                    OnParseError("Table start tag implies end of previous table");
                    // XXX: do not process an implied end tag if we know p-front that it'll be ignored
                    if (IsInScope("table", true)) {
                        OnParseError("???");
                        return CurrentTokenizerTokenState.Ignored;
                    } else {
                        return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateEndTag("table"));
                    }
                default:
                    OnParseError("???");
                    // XXX: the "in table" exception will be handled in the "real tree builder algorithm"
                    return ParseMainInBody();
                }
            case XmlNodeType.EndElement:
                switch (_tokenizer.Name) {
                case "table":
                    if (IsInScope("table", true)) {
                        OnParseError("???");
                        return CurrentTokenizerTokenState.Ignored;
                    } else if (GenerateImpliedEndTags(null)) {
                        return CurrentTokenizerTokenState.Unprocessed;
                    } else {
                        if (_openElements.First.Value.name != "table") {
                            OnParseError("Table end tag seen too early");
                            while (_openElements.First.Value.name != "table") {
                                _pendingOutputTokens.Enqueue(Token.CreateEndTag(_openElements.First.Value.name));
                                _openElements.RemoveFirst();
                            }
                        }
                        // Pop the table element
                        _openElements.RemoveFirst();
                        return CurrentTokenizerTokenState.Emitted;
                    }
                case "body":
                case "caption":
                case "col":
                case "colgroup":
                case "html":
                case "tbody":
                case "td":
                case "tfoot":
                case "th":
                case "thead":
                case "tr":
                    OnParseError(String.Concat("Found ", _tokenizer.Name, " end tag, expected table."));
                    return CurrentTokenizerTokenState.Ignored;
                default:
                    OnParseError("???");
                    // XXX: the "in table" exception will be handled in the "real tree builder algorithm"
                    return ParseMainInBody();
                }
            default:
                throw new InvalidOperationException(
                    String.Concat("Unexpected token type: ",
                        Enum.GetName(typeof(XmlNodeType), _tokenizer.TokenType)));
            }
        }

        private CurrentTokenizerTokenState ParseMainInCaption()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#in-caption
            switch (_tokenizer.TokenType) {
            case XmlNodeType.Text:
            case XmlNodeType.Whitespace:
            case XmlNodeType.Comment:
                return ParseMainInBody();
            case XmlNodeType.Element:
                switch (_tokenizer.Name) {
                case "caption":
                case "col":
                case "colgroup":
                case "tbody":
                case "td":
                case "tfoot":
                case "th":
                case "thead":
                case "tr":
                    OnParseError(String.Concat("Found ", _tokenizer.Name, " start tag, implies caption end tag."));
                    // XXX: do not generate an implied end tag if we knowup-frant that it'll be ignored
                    if (IsInScope("caption", true)) {
                        OnParseError("???");
                        return CurrentTokenizerTokenState.Ignored;
                    } else {
                        return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateEndTag("caption"));
                    }
                default:
                    return ParseMainInBody();
                }
            case XmlNodeType.EndElement:
                switch (_tokenizer.Name) {
                case "caption":
                    if (IsInScope("caption", true)) {
                        OnParseError("???");
                        return CurrentTokenizerTokenState.Ignored;
                    } else if (GenerateImpliedEndTags(null)) {
                        return CurrentTokenizerTokenState.Unprocessed;
                    } else {
                        if (_openElements.First.Value.name != "caption") {
                            OnParseError("???");
                            while (_openElements.First.Value.name != "caption") {
                                _pendingOutputTokens.Enqueue(Token.CreateEndTag(_openElements.First.Value.name));
                                _openElements.RemoveFirst();
                            }
                        }
                        // Pop the caption element
                        _openElements.RemoveFirst();
                        ClearActiveFormattingElements();
                        _insertionMode = InsertionMode.InTable;
                        return CurrentTokenizerTokenState.Emitted;
                    }
                case "table":
                    OnParseError(String.Concat("Found ", _tokenizer.Name, " start tag, implies caption end tag."));
                    // XXX: do not generate an implied end tag if we knowup-frant that it'll be ignored
                    if (IsInScope("caption", true)) {
                        OnParseError("???");
                        return CurrentTokenizerTokenState.Ignored;
                    } else {
                        return ActAsIfTokenHadBeenSeenThenReprocessCurrentToken(Token.CreateEndTag("caption"));
                    }
                case "body":
                case "col":
                case "colgroup":
                case "html":
                case "tbody":
                case "td":
                case "tfoot":
                case "th":
                case "thead":
                case "tr":
                    OnParseError("???");
                    return CurrentTokenizerTokenState.Ignored;
                default:
                    return ParseMainInBody();
                }
            default:
                throw new InvalidOperationException(
                    String.Concat("Unexpected token type: ",
                        Enum.GetName(typeof(XmlNodeType), _tokenizer.TokenType)));
            }
        }

        // TODO: table-related and InSelect insertion modes

        private CurrentTokenizerTokenState ParseMainAfterBody()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#after4
            switch (_tokenizer.TokenType) {
            case XmlNodeType.Whitespace:
                // XXX: Process the token as it would be processed if the insertion mode was "in body".
                return ParseMainInBody();
            case XmlNodeType.Comment:
                // TODO: keep the token somewhere, waiting for </html>
                return CurrentTokenizerTokenState.Emitted;
            case XmlNodeType.Text:
                string whitespace = ExtractLeadingWhitespace();
                if (whitespace.Length > 0) {
                    _tokenizer.PushToken(Token.CreateWhitespace(whitespace));
                    goto case XmlNodeType.Whitespace;
                }
                goto case XmlNodeType.Element;
            case XmlNodeType.EndElement:
                if (_tokenizer.Name == "html") {
                    // XXX: this is where we emit the </body>
                    Debug.Assert(_openElements.First.Value.name == "body");
                    _openElements.RemoveFirst();
                    _pendingOutputTokens.Enqueue(Token.CreateEndTag("body"));
                    Debug.Assert(_openElements.First.Value.name == "html");
                    if (_fragmentCase) {
                        OnParseError("Unexpected html end tag in innerHTML");
                    } else {
                        _phase = TreeConstructionPhase.TrailingEnd;
                    }
                    // In either case, ignore the token: in the non-fragment case, the </html> will be emitted in the trailing end and/or at EOF
                    return CurrentTokenizerTokenState.Ignored;
                }
                goto case XmlNodeType.Element;
            case XmlNodeType.Element:
                OnParseError("Non-space characters seen after the body.");
                _insertionMode = InsertionMode.InBody;
                return CurrentTokenizerTokenState.Unprocessed;
            default:
                throw new InvalidOperationException(
                    String.Concat("Unexpected token type: ",
                        Enum.GetName(typeof(XmlNodeType), _tokenizer.TokenType)));
            }
        }

        private CurrentTokenizerTokenState ParseTrailingEnd()
        {
            // http://www.whatwg.org/specs/web-apps/current-work/multipage/section-tree-construction.html#the-trailing
            switch (_tokenizer.TokenType) {
            case XmlNodeType.DocumentType:
                OnParseError("DOCTYPE found near the end of the document.");
                return CurrentTokenizerTokenState.Ignored;
            case XmlNodeType.Comment:
                return CurrentTokenizerTokenState.Emitted;
            case XmlNodeType.Whitespace:
                // XXX: Process the token as it would be processed in the main phase.
                return ParseMain();
            case XmlNodeType.Text:
            case XmlNodeType.Element:
            case XmlNodeType.EndElement:
                // XXX: that's not exact what the draft says wrt leading whitespace in text tokens but it's exactly equivalent
                OnParseError("Non-space characters in the trailing end");
                _phase = TreeConstructionPhase.Main;
                return CurrentTokenizerTokenState.Unprocessed;
            default:
                throw new InvalidOperationException(
                    String.Concat("Unexpected token type: ",
                        Enum.GetName(typeof(XmlNodeType), _tokenizer.TokenType)));
            }
        }
    }
}
