namespace AElf.CrossChain
{
    public class CrossChainConstants
    {
        public const int WaitingIntervalInMillisecond = 10;
        public const int MaximalCountForIndexingParentChainBlock = 256; // Maximal count for once indexing from parent chain.
        public const int MaximalCountForIndexingSideChainBlock = 256; // Maximal count for once indexing from side chain.
        public const int MinimalBlockCacheEntityCount = 4 * 8;
        public const string CrossChainIndexingMethodName = "RecordCrossChainData";
    }
}