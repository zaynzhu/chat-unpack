var suite = TestSuite()

runTimestampParserTests(&suite)
runMessageParserTests(&suite)
runOverlapMatcherTests(&suite)
runTranscriptAssemblerTests(&suite)
runMarkdownRendererTests(&suite)

suite.finish()
