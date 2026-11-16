using LMLocal.Infrastructure.Autocompletions.InlineCompletion;
using NUnit.Framework;

namespace LMLocal.Tests.Unit.Infrastructure.InlineCompletion
{
    /// <summary>
    /// Formatting of the "last visited caret line" annotation that is prepended to the FIM prefix.
    /// </summary>
    [TestFixture]
    public class RecentCaretContextTests
    {
        [SetUp]
        public void SetUp()
        {
            RecentCaretContext.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            RecentCaretContext.Clear();
        }

        [Test]
        public void SameFile_UsesLineNumberOnly()
        {
            RecentCaretContext.Record(@"C:\proj\A.cs", 42, "    ApplyForce();");

            var annotation = RecentCaretContext.BuildAnnotation(@"C:\proj\A.cs", 100, "prefix", "suffix");

            Assert.That(annotation, Is.EqualTo("// recently visited line 42: ApplyForce();"));
        }

        [Test]
        public void OtherFile_IncludesFileNameAndLine()
        {
            RecentCaretContext.Record(@"C:\proj\Utils.cs", 42, "var joint = CreateJoint();");

            var annotation = RecentCaretContext.BuildAnnotation(@"C:\proj\A.cs", 10, "", "");

            Assert.That(annotation, Is.EqualTo("// recently visited Utils.cs:42: var joint = CreateJoint();"));
        }

        [Test]
        public void Python_UsesHashComment()
        {
            RecentCaretContext.Record(@"C:\proj\B.py", 7, "x = compute(a)");

            var annotation = RecentCaretContext.BuildAnnotation(@"C:\proj\A.py", 1, "", "");

            Assert.That(annotation, Is.EqualTo("# recently visited B.py:7: x = compute(a)"));
        }

        [Test]
        public void Markup_UsesBlockComment()
        {
            RecentCaretContext.Record(@"C:\proj\B.html", 7, "<div>");

            var annotation = RecentCaretContext.BuildAnnotation(@"C:\proj\A.xaml", 1, "", "");

            Assert.That(annotation, Is.EqualTo("<!-- recently visited B.html:7: <div> -->"));
        }

        [Test]
        public void CurrentLine_ReturnsNull()
        {
            RecentCaretContext.Record(@"C:\proj\A.cs", 42, "ApplyForce();");

            Assert.That(RecentCaretContext.BuildAnnotation(@"C:\proj\A.cs", 42, "", ""), Is.Null);
        }

        [Test]
        public void LineAlreadyInPrefixOrSuffix_ReturnsNull()
        {
            RecentCaretContext.Record(@"C:\proj\A.cs", 42, "ApplyForce();");

            Assert.That(RecentCaretContext.BuildAnnotation(@"C:\proj\A.cs", 100, "void Update() { ApplyForce(); }", ""), Is.Null);
            Assert.That(RecentCaretContext.BuildAnnotation(@"C:\proj\A.cs", 100, "", "ApplyForce();"), Is.Null);
        }

        [Test]
        public void RecentLineInsidePrefix_FallsBackToOlderVisit()
        {
            RecentCaretContext.Record(@"C:\proj\Utils.cs", 42, "var joint = CreateJoint();");
            RecentCaretContext.Record(@"C:\proj\A.cs", 11, "    return ApplyFor");

            var annotation = RecentCaretContext.BuildAnnotation(
                @"C:\proj\A.cs", 12, "void Update() {\r\n    return ApplyFor();\r\n}", "");

            Assert.That(annotation, Is.EqualTo("// recently visited Utils.cs:42: var joint = CreateJoint();"));
        }

        [Test]
        public void WhitespaceLine_IsNotRecorded()
        {
            RecentCaretContext.Record(@"C:\proj\A.cs", 42, "ApplyForce();");
            RecentCaretContext.Record(@"C:\proj\A.cs", 43, "   ");

            Assert.That(RecentCaretContext.Last.LineNumber, Is.EqualTo(42));
        }

        [Test]
        public void TrivialLine_ReturnsNull()
        {
            RecentCaretContext.Record(@"C:\proj\A.cs", 42, "}");

            Assert.That(RecentCaretContext.BuildAnnotation(@"C:\proj\A.cs", 100, "", ""), Is.Null);
        }

        [Test]
        public void LongLine_IsTruncated()
        {
            RecentCaretContext.Record(@"C:\proj\A.cs", 42, new string('x', 500));

            var annotation = RecentCaretContext.BuildAnnotation(@"C:\proj\A.cs", 100, "", "");

            Assert.That(annotation, Does.EndWith("…"));
            Assert.That(annotation.Length, Is.LessThan(260));
        }

        [Test]
        public void CacheKey_DiffersByRecentLineContext()
        {
            var withContext = CompletionCache.BuildKey("A.cs", 1, 1, "prefix", "suffix", "// recently visited line 42: x();");
            var withoutContext = CompletionCache.BuildKey("A.cs", 1, 1, "prefix", "suffix");

            Assert.That(withContext, Is.Not.EqualTo(withoutContext));
        }
    }
}
