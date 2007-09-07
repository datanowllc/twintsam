using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Xml;

using Twintsam.Html;
using System.IO;


namespace Twintsam.TestApp
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>

    public partial class Window1 : System.Windows.Window
    {

        public Window1()
        {
            InitializeComponent();
        }

        public void ParseHtml(object sender, RoutedEventArgs e)
        {
            Tokens.Items.Clear();
            ReconstructedHTML.Clear();
            ParseErrors.Items.Clear();
            HtmlReader reader = new HtmlReader(new Tokenizer(new HtmlTextTokenizer(new StringReader(HtmlInput.Text)), Tokens));
            reader.ParseError += new EventHandler<ParseErrorEventArgs>(reader_ParseError);
            while (reader.Read()) {
                switch (reader.NodeType) {
                case XmlNodeType.Text:
                case XmlNodeType.Whitespace:
                case XmlNodeType.SignificantWhitespace:
                    ReconstructedHTML.AppendText(reader.Value);
                    break;
                case XmlNodeType.Comment:
                    ReconstructedHTML.AppendText("<!--");
                    ReconstructedHTML.AppendText(reader.Value);
                    ReconstructedHTML.AppendText("-->");
                    break;
                case XmlNodeType.Element:
                    ReconstructedHTML.AppendText("<");
                    ReconstructedHTML.AppendText(reader.Name);
                    if (reader.MoveToFirstAttribute()) {
                        do {
                            ReconstructedHTML.AppendText(" ");
                            ReconstructedHTML.AppendText(reader.Name);
                            ReconstructedHTML.AppendText("=");
                            ReconstructedHTML.AppendText(reader.QuoteChar.ToString());
                            ReconstructedHTML.AppendText(reader.Value);
                            ReconstructedHTML.AppendText(reader.QuoteChar.ToString());
                        } while (reader.MoveToNextAttribute());
                    }
                    ReconstructedHTML.AppendText(">");
                    break;
                case XmlNodeType.EndElement:
                    ReconstructedHTML.AppendText("</");
                    ReconstructedHTML.AppendText(reader.Name);
                    ReconstructedHTML.AppendText(">");
                    break;
                default:
                    ReconstructedHTML.AppendText("###");
                    ReconstructedHTML.AppendText(Enum.GetName(typeof(XmlNodeType), reader.NodeType));
                    ReconstructedHTML.AppendText("###");
                    break;
                }
            }
        }

        void reader_ParseError(object sender, ParseErrorEventArgs e)
        {
            ParseErrors.Items.Add(e);
        }

        private class Tokenizer : HtmlWrappingTokenizer
        {
            private readonly ListView _output;

            public Tokenizer(HtmlTokenizer tokenizer, ListView output)
                : base(tokenizer)
            {
                _output = output;
            }

            public override bool Read()
            {
                if (base.Read())
                {
                    switch (TokenType)
                    {
                    case XmlNodeType.Text:
                        _output.Items.Add(
                            new KeyValuePair<string,string>("Text", Value));
                        break;
                    case XmlNodeType.Whitespace:
                        _output.Items.Add(
                            new KeyValuePair<string, string>("Whitespace", Value));
                        break;
                    case XmlNodeType.Comment:
                        _output.Items.Add(
                            new KeyValuePair<string, string>("Comment", Value));
                        break;
                    case XmlNodeType.Element:
                        _output.Items.Add(
                            new KeyValuePair<string, string>("Start tag", Name));
                        break;
                    case XmlNodeType.EndElement:
                        _output.Items.Add(
                            new KeyValuePair<string, string>("End tag", Name));
                        break;
                    default:
                        ListViewItem item = new ListViewItem();
                        _output.Items.Add(
                            new KeyValuePair<string, string>("Unknown", "Token"));
                        break;
                    }
                    return true;
                }
                return false;
            }
        }
    }
}