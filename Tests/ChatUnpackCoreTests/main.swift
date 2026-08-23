var suite = TestSuite()

runTimestampParserTests(&suite)
runScrollPositionTests(&suite)
runMessageParserTests(&suite)
runOverlapMatcherTests(&suite)
runTranscriptAssemblerTests(&suite)
runMarkdownRendererTests(&suite)
runMarkdownChunkerTests(&suite)

suite.finish()
