﻿using System.Threading.Tasks;

namespace AElf.Kernel.Node
{
    public interface IP2P
    {
        Task ProcessLoop();
        Task<bool> BroadcastBlock(IBlock block);
    }
}