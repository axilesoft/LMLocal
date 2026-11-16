using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;

namespace LMLocal.Infrastructure.Autocompletions.InlineCompletion
{
    /// <summary>
    /// Intercepts Ctrl+Right (word next) and Ctrl+Down (scroll line down) while a ghost-text
    /// suggestion is shown, accepting one word / one line instead. Otherwise passes through.
    /// </summary>
    internal sealed class SuggestionCommandFilter : IOleCommandTarget
    {
        // VS inline suggestions command set (Edit.AcceptNextWordinSuggestion / ...NextLineinSuggestion).
        private static readonly Guid InlineSuggestionsCmdSet = new Guid("160961B3-909D-4B28-9353-A1BEF587B4A6");
        private const uint InlineAcceptNextWordCommandId = 100;
        private const uint InlineAcceptNextLineCommandId = 101;

        private readonly SuggestionTagger _tagger;

        internal IOleCommandTarget Next { get; set; }

        internal SuggestionCommandFilter(SuggestionTagger tagger)
        {
            _tagger = tagger ?? throw new ArgumentNullException(nameof(tagger));
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return Next != null
                ? Next.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText)
                : (int)Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED;
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_tagger.HasSuggestion)
            {
                bool inlineGroup = pguidCmdGroup == InlineSuggestionsCmdSet;
                bool std2kGroup = pguidCmdGroup == VSConstants.VSStd2K;

                if ((std2kGroup && nCmdID == (uint)VSConstants.VSStd2KCmdID.WORDNEXT) ||
                    (inlineGroup && nCmdID == InlineAcceptNextWordCommandId))
                {
                    _tagger.AcceptNextWord();
                    return VSConstants.S_OK;
                }

                if ((std2kGroup && nCmdID == (uint)VSConstants.VSStd2KCmdID.SCROLLDN) ||
                    (inlineGroup && nCmdID == InlineAcceptNextLineCommandId))
                {
                    _tagger.AcceptNextLine();
                    return VSConstants.S_OK;
                }
            }

            return Next != null
                ? Next.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut)
                : (int)Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED;
        }
    }
}
