using ChatUnpack.Core.TestRunner;

var suite = new TestSuite();

TimestampParserTests.Run(suite);
ScrollPositionTests.Run(suite);
MessageParserTests.Run(suite);
OverlapMatcherTests.Run(suite);
TranscriptAssemblerTests.Run(suite);
MarkdownRendererTests.Run(suite);
MarkdownChunkerTests.Run(suite);

return suite.Finish();
