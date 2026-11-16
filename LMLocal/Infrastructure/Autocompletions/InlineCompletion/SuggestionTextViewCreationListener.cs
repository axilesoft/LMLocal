using System;
using System.ComponentModel.Composition;
using System.Windows.Input;
using LMLocal.Core.Common;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Suggestions;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;

namespace LMLocal.Infrastructure.Autocompletions.InlineCompletion
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("text")]
    [ContentType("code")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal class SuggestionTextViewCreationListener : IWpfTextViewCreationListener
    {
        [Import]
        internal ICompletionBroker CompletionBroker { get; set; } = null;

        [Import]
        internal IEditorOperationsFactoryService EditorOperationsFactory { get; set; } = null;

        // VS inline suggestions (Copilot, IntelliCode). Optional: absent on VS builds without the service.
        [Import(AllowDefault = true)]
        internal SuggestionServiceBase SuggestionService { get; set; } = null;

        // Modern (async) IntelliSense completion list, used by C# and most languages in VS 2022.
        [Import(AllowDefault = true)]
        internal IAsyncCompletionBroker AsyncCompletionBroker { get; set; } = null;

        [Import(AllowDefault = true)]
        internal IVsEditorAdaptersFactoryService EditorAdaptersFactory { get; set; } = null;

        internal static readonly CompletionCache SharedCache = new CompletionCache();

        public void TextViewCreated(IWpfTextView textView)
        {
            if (textView == null)
                return;

            if (textView.Roles.Contains(PredefinedTextViewRoles.PreviewTextView) || textView.Roles.Contains("COMMANDVIEW"))
                return;

            if (!textView.TextBuffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document))
                return;

            string path = document.FilePath;
            if (string.IsNullOrEmpty(path))
                return;


            var editorOperations = EditorOperationsFactory?.GetEditorOperations(textView);
            var tagger = textView.Properties.GetOrCreateSingletonProperty(
                typeof(SuggestionTagger),
                () => new SuggestionTagger(textView, SharedCache, CompletionBroker, editorOperations, SuggestionService, AsyncCompletionBroker));

            IVsTextView viewAdapter = null;
            SuggestionCommandFilter commandFilter = null;
            if (EditorAdaptersFactory != null)
            {
                viewAdapter = EditorAdaptersFactory.GetViewAdapter(textView);
                if (viewAdapter != null)
                {
                    commandFilter = new SuggestionCommandFilter(tagger);
                    viewAdapter.AddCommandFilter(commandFilter, out IOleCommandTarget next);
                    commandFilter.Next = next;
                }
            }

            void OnTextBufferChanged(object s, TextContentChangedEventArgs e)
            {
                try
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    tagger.HandleTextChanged(e);
                }
                catch (Exception ex)
                {
                    InternalLogger.Warn("bufferHandler: " + ex.ToString());
                }
            }

            void OnCaretPositionChanged(object s, CaretPositionChangedEventArgs e)
            {
                try
                {
                    ThreadHelper.ThrowIfNotOnUIThread();

                    if (textView.HasAggregateFocus)
                    {
                        var oldPoint = e.OldPosition.BufferPosition;
                        if (oldPoint.GetContainingLine().LineNumber != e.NewPosition.BufferPosition.GetContainingLine().LineNumber)
                            RecentCaretContext.Record(oldPoint.Snapshot, oldPoint.Position);
                    }

                    tagger.HandleCaretMoved(e);
                }
                catch (Exception ex)
                {
                    InternalLogger.Warn("caretHandler: " + ex.ToString());
                }
            }

            void OnLostAggregateFocus(object s, EventArgs e)
            {
                try
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    var point = textView.Caret.Position.BufferPosition;
                    RecentCaretContext.Record(point.Snapshot, point.Position);
                }
                catch (Exception ex)
                {
                    InternalLogger.Warn("focusHandler: " + ex.ToString());
                }
            }

            void OnGotAggregateFocus(object s, EventArgs e)
            {
                SuggestionTagger.SetFocused(tagger);
            }

            void OnPreviewKeyDown(object s, KeyEventArgs e)
            {
                try
                {
                    var key = e.Key == Key.System ? e.SystemKey : e.Key;
                    if (key == Key.OemQuotes && Keyboard.Modifiers == ModifierKeys.Alt)
                    {
                        tagger.TriggerSuggestion();
                        e.Handled = true;
                    }
                }
                catch (Exception ex)
                {
                    InternalLogger.Warn("keyHandler: " + ex.ToString());
                }
            }

            void OnLayoutChanged(object s, TextViewLayoutChangedEventArgs e)
            {
                try
                {
                    tagger.HandleLayoutChanged(e);
                }
                catch (Exception ex)
                {
                    InternalLogger.Warn("layoutHandler: " + ex.ToString());
                }
            }

            textView.TextBuffer.Changed += OnTextBufferChanged;
            textView.Caret.PositionChanged += OnCaretPositionChanged;
            textView.LayoutChanged += OnLayoutChanged;
            textView.LostAggregateFocus += OnLostAggregateFocus;
            textView.GotAggregateFocus += OnGotAggregateFocus;

            var visualElement = textView.VisualElement;
            if (visualElement != null)
                visualElement.PreviewKeyDown += OnPreviewKeyDown;

            if (textView.HasAggregateFocus)
                SuggestionTagger.SetFocused(tagger);

            textView.Closed += (s, e) =>
            {
                textView.TextBuffer.Changed -= OnTextBufferChanged;
                textView.Caret.PositionChanged -= OnCaretPositionChanged;
                textView.LayoutChanged -= OnLayoutChanged;
                textView.LostAggregateFocus -= OnLostAggregateFocus;
                textView.GotAggregateFocus -= OnGotAggregateFocus;

                if (visualElement != null)
                    visualElement.PreviewKeyDown -= OnPreviewKeyDown;

                if (viewAdapter != null && commandFilter != null)
                    viewAdapter.RemoveCommandFilter(commandFilter);

                if (ReferenceEquals(SuggestionTagger.Focused, tagger))
                    SuggestionTagger.SetFocused(null);

                if (textView.Properties.TryGetProperty(typeof(SuggestionTagger), out SuggestionTagger t))
                {
                    t.Dispose();
                    textView.Properties.RemoveProperty(typeof(SuggestionTagger));
                }
            };
        }
    }
}

