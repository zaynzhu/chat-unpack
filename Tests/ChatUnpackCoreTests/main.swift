var suite = TestSuite()

runTimestampParserTests(&suite)
runScrollPositionTests(&suite)
runMessageParserTests(&suite)
runOverlapMatcherTests(&suite)
runTranscriptAssemblerTests(&suite)
runMarkdownRendererTests(&suite)

suite.finish()
