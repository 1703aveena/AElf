namespace AElf.Crosschain.Grpc
{
    public class TypeConsts
    {
        public const int WaitingIntervalInMillisecond = 10;
        public const int MaximalCountForIndexingParentChainBlock = 256; // Index maximal 256 blocks from parent chain.
        public const int MaximalCountForIndexingSideChainBlock = 1; // Index maximal one block from one side chain.
        public static int MinimalBlockInfoCacheThreshold = 4; // This is the biggest LIB gap actually.
    }
}