#nullable enable

using System;
using System.Collections.Generic;

using Foundation;
using UIKit;

using Microsoft.Scripting.Hosting;
using IronPython.Hosting;
using System.Threading.Tasks;

namespace PythonRepl
{
    public class PythonReplViewController : UIViewController
    {
        static readonly UIFont CodeFont = UIFont.FromName("Menlo", (nfloat)(UIFont.SystemFontSize * 1.2));

        readonly Lazy<(ScriptEngine Engine, ScriptScope Scope)> python = new Lazy<(ScriptEngine, ScriptScope)>(() =>
        {
            var eng = Python.CreateEngine();
            var scope = eng.CreateScope();
            return (eng, scope);
        });

        readonly UITextField inputField = new UITextField
        {
            Font = CodeFont,
            TranslatesAutoresizingMaskIntoConstraints = false,
            AutocorrectionType = UITextAutocorrectionType.No,
            AutocapitalizationType = UITextAutocapitalizationType.None,
            SmartQuotesType = UITextSmartQuotesType.No,
            SmartDashesType = UITextSmartDashesType.No,
            SmartInsertDeleteType = UITextSmartInsertDeleteType.No,
        };
        readonly UIButton inputButton = UIButton.FromType(UIButtonType.RoundedRect);
        readonly UIStackView inputPanel = new UIStackView
        {
            TranslatesAutoresizingMaskIntoConstraints = false,
            Axis = UILayoutConstraintAxis.Horizontal,
            Alignment = UIStackViewAlignment.FirstBaseline,
            Distribution = UIStackViewDistribution.Fill,
        };

        readonly UITableView historyView = new UITableView(UIScreen.MainScreen.Bounds, UITableViewStyle.Plain);

        readonly HistoryData history = new HistoryData();

        public PythonReplViewController()
        {
            inputButton.TranslatesAutoresizingMaskIntoConstraints = false;
            inputButton.SetTitle("Run", UIControlState.Normal);
            inputPanel.AddArrangedSubview(inputField);
            inputPanel.AddArrangedSubview(inputButton);
            Title = "Python in Xamarin";
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            var view = this.View;
            if (view is null) return;
            view.AddSubview(historyView);
            view.AddSubview(inputPanel);

            historyView.DataSource = history;
            historyView.TranslatesAutoresizingMaskIntoConstraints = false;

            inputField.BackgroundColor = UIColor.SystemBackground;
            inputPanel.BackgroundColor = UIColor.TertiarySystemBackground;

            view.AddConstraints(new[] {
                NSLayoutConstraint.Create(view.LayoutMarginsGuide, NSLayoutAttribute.Left, NSLayoutRelation.Equal, historyView, NSLayoutAttribute.Left, 1, 0),
                NSLayoutConstraint.Create(view.LayoutMarginsGuide, NSLayoutAttribute.Right, NSLayoutRelation.Equal, historyView, NSLayoutAttribute.Right, 1, 0),
                NSLayoutConstraint.Create(view, NSLayoutAttribute.Top, NSLayoutRelation.Equal, historyView, NSLayoutAttribute.Top, 1, 0),
                NSLayoutConstraint.Create(historyView, NSLayoutAttribute.Bottom, NSLayoutRelation.Equal, inputPanel, NSLayoutAttribute.Top, 1, 0),

                NSLayoutConstraint.Create(view, NSLayoutAttribute.Left, NSLayoutRelation.Equal, inputPanel, NSLayoutAttribute.Left, 1, 0),
                NSLayoutConstraint.Create(view, NSLayoutAttribute.Right, NSLayoutRelation.Equal, inputPanel, NSLayoutAttribute.Right, 1, 0),
                view.KeyboardLayoutGuide.TopAnchor.ConstraintEqualTo(inputPanel.BottomAnchor),
                NSLayoutConstraint.Create(inputPanel, NSLayoutAttribute.Height, NSLayoutRelation.Equal, inputField, NSLayoutAttribute.Height, 1, 22),
            });

            inputField.ShouldReturn = (x) =>
            {
                HandleInput();
                return false;
            };
            inputButton.TouchUpInside += (x, y) => HandleInput();
        }

        public override void ViewWillAppear(bool animated)
        {
            base.ViewWillAppear(animated);
            inputField.BecomeFirstResponder();
        }

        async void HandleInput()
        {
            var initialItemCount = history.RowsInSection(historyView, 0);
            var newItem = new HistoryItem(inputField.Text ?? "");
            if (!string.IsNullOrWhiteSpace(newItem.Code))
            {
                inputField.AttributedText = PrettyPrint("");
                history.AddInput(newItem);
                var indexPath = NSIndexPath.FromRowSection(initialItemCount, 0);
                historyView.InsertRows(new[] { indexPath }, UITableViewRowAnimation.Bottom);
                historyView.ScrollToRow(indexPath, UITableViewScrollPosition.Bottom, true);
                newItem.Result = (await Task.Run(() =>
                {
                    try
                    {
                        var (eng, scope) = python.Value;
                        var r = eng.Execute(newItem.Code, scope);
                        return Repr(r, eng, scope);
                    }
                    catch (Exception ex)
                    {
                        return ex.Message;
                    }
                }));
                historyView.ReloadRows(new[] { indexPath }, UITableViewRowAnimation.None);
            }
            else if (initialItemCount > 0)
            {
                var indexPath = NSIndexPath.FromRowSection(initialItemCount - 1, 0);
                historyView.ScrollToRow(indexPath, UITableViewScrollPosition.Bottom, true);
            }
        }

        static string? Repr(dynamic value, ScriptEngine eng, ScriptScope scope)
        {
            if (value is null) return null;
            if (value is string s) return $"\"{s}\"";

            scope.SetVariable("__r__", value);
            var repr = eng.Execute("repr(__r__)", scope);

            return repr.ToString();
        }

        static NSAttributedString PrettyPrint(string code)
        {
            var astring = new NSMutableAttributedString(code);
            var n = code.Length;
            astring.AddAttribute(UIStringAttributeKey.Font, CodeFont, new NSRange(0, n));
            return astring;
        }

        class HistoryItem
        {
            public string Code;
            public string? Result;

            public HistoryItem(string code, string? result = null)
            {
                Code = code;
                Result = result;
            }
        }

        class HistoryData : UITableViewDataSource
        {
            List<HistoryItem> items = new List<HistoryItem>();

            public HistoryData()
            {
            }

            public void AddInput(HistoryItem item)
            {
                items.Add(item);
            }

            public override nint NumberOfSections(UITableView tableView)
            {
                return 1;
            }

            public override nint RowsInSection(UITableView tableView, nint section)
            {
                return items.Count;
            }

            public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
            {
                var cell = tableView.DequeueReusableCell("C");
                if (cell is null)
                {
                    cell = new UITableViewCell(UITableViewCellStyle.Default, "C");
                    cell.TextLabel.Font = CodeFont;
                }
                var rowIndex = (int)indexPath.Row;
                var item = items[rowIndex];
                if (item.Result is string r)
                {
                    cell.TextLabel.AttributedText = PrettyPrint(item.Code + " = " + r);
                }
                else
                {
                    cell.TextLabel.AttributedText = PrettyPrint(item.Code);
                }
                return cell;
            }
        }
    }
}

