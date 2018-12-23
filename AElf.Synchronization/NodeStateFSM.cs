using System.Threading;
using AElf.Common;
using AElf.Common.FSM;
using AElf.Synchronization.EventMessages;
using Easy.MessageHub;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AElf.Synchronization
{
    // ReSharper disable InconsistentNaming
    public class NodeStateFSM
    {

        public NodeStateFSM()
        {
            Logger = NullLogger<NodeStateFSM>.Instance;
        }
        
        public ILogger<NodeStateFSM> Logger { get; set; }

        private FSM<NodeState> _fsm;

        private bool _caught;
        
        private int _inAState;

        public FSM<NodeState>Create()
        {
            _fsm = new FSM<NodeState>();
            AddStates();
            _fsm.CurrentState = NodeState.Catching;
            return _fsm;
        }

        private void AddStates()
        {
            AddCatching();
            AddCaught();
            AddBlockValidating();
            AddBlockExecuting();
            AddBlockAppending();
            AddGeneratingConsensusTx();
            AddProducingBlock();
            AddExecutingLoop();
            AddReverting();
        }

        private void AddCatching()
        {
            NodeState TransferFromCatching()
            {
                if (_fsm.StateEvent == StateEvent.ValidBlockHeader)
                {
                    return NodeState.BlockValidating;
                }

                if (_fsm.StateEvent == StateEvent.MiningStart)
                {
                    _caught = true;
                    return NodeState.GeneratingConsensusTx;
                }

                return NodeState.Stay;
            }

            _fsm.AddState(NodeState.Catching)
                .SetTransferFunction(TransferFromCatching)
                .OnEntering(WhenEnteringState)
                .OnLeaving(WhenLeavingState);
        }

        private void AddCaught()
        {
            NodeState TransferFromCaught()
            {
                if (_fsm.StateEvent == StateEvent.ValidBlockHeader)
                {
                    return NodeState.BlockValidating;
                }

                if (_fsm.StateEvent == StateEvent.MiningStart)
                {
                    return NodeState.GeneratingConsensusTx;
                }

                if (_fsm.StateEvent == StateEvent.LongerChainDetected)
                {
                    return NodeState.Reverting;
                }

                return NodeState.Stay;
            }

            _fsm.AddState(NodeState.Caught)
                .SetTransferFunction(TransferFromCaught)
                .OnEntering(WhenEnteringState)
                .OnLeaving(WhenLeavingState);
        }

        private void AddBlockValidating()
        {
            NodeState TransferFromBlockValidating()
            {
                if (_fsm.StateEvent == StateEvent.ValidBlock)
                {
                    return NodeState.BlockExecuting;
                }

                if (_fsm.StateEvent == StateEvent.InvalidBlock)
                {
                    return _caught ? NodeState.Caught : NodeState.Catching;
                }

                if (_fsm.StateEvent == StateEvent.LongerChainDetected && _caught)
                {
                    return NodeState.Reverting;
                }

                return NodeState.Stay;
            }

            _fsm.AddState(NodeState.BlockValidating)
                .SetTransferFunction(TransferFromBlockValidating)
                .OnEntering(WhenEnteringState)
                .OnLeaving(WhenLeavingState);
        }

        private void AddBlockExecuting()
        {
            NodeState TransferFromBlockExecuting()
            {
                if (_fsm.StateEvent == StateEvent.StateUpdated)
                {
                    return NodeState.BlockAppending;
                }

                if (_fsm.StateEvent == StateEvent.StateNotUpdated)
                {
                    return NodeState.ExecutingLoop;
                }

                if (_fsm.StateEvent == StateEvent.LongerChainDetected && _caught)
                {
                    return NodeState.Reverting;
                }

                if (_fsm.StateEvent == StateEvent.BlockAppended)
                {
                    return _caught ? NodeState.Caught : NodeState.Catching;
                }

                return NodeState.Stay;
            }

            _fsm.AddState(NodeState.BlockExecuting)
                .SetTransferFunction(TransferFromBlockExecuting)
                .OnEntering(WhenEnteringState)
                .OnLeaving(WhenLeavingState);
        }

        private void AddBlockAppending()
        {
            NodeState TransferFromAddBlockAppending()
            {
                if (_fsm.StateEvent == StateEvent.BlockAppended)
                {
                    return _caught ? NodeState.Caught : NodeState.Catching;
                }

                return NodeState.Stay;
            }

            _fsm.AddState(NodeState.BlockAppending)
                .SetTransferFunction(TransferFromAddBlockAppending)
                .OnEntering(WhenEnteringState)
                .OnLeaving(WhenLeavingState);
        }

        private void AddGeneratingConsensusTx()
        {
            NodeState TransferFromAddGeneratingConsensusTx()
            {
                if (_fsm.StateEvent == StateEvent.ConsensusTxGenerated)
                {
                    return NodeState.ProducingBlock;
                }

                if (_fsm.StateEvent == StateEvent.LongerChainDetected)
                {
                    return NodeState.Reverting;
                }

                if (_fsm.StateEvent == StateEvent.MiningEnd)
                {
                    return NodeState.Caught;
                }

                return NodeState.Stay;
            }

            _fsm.AddState(NodeState.GeneratingConsensusTx)
                .SetTransferFunction(TransferFromAddGeneratingConsensusTx)
                .OnEntering(WhenEnteringState)
                .OnLeaving(WhenLeavingState);
        }

        private void AddProducingBlock()
        {
            NodeState TransferFromAddProducingBlock()
            {
                if (_fsm.StateEvent == StateEvent.MiningEnd)
                {
                    return NodeState.Caught;
                }

                return NodeState.Stay;
            }

            _fsm.AddState(NodeState.ProducingBlock)
                .SetTransferFunction(TransferFromAddProducingBlock)
                .OnEntering(WhenEnteringState)
                .OnLeaving(WhenLeavingState);
        }

        private void AddExecutingLoop()
        {
            NodeState TransferFromAddExecutingLoop()
            {
                if (_fsm.StateEvent == StateEvent.ValidBlockHeader)
                {
                    return NodeState.ExecutingLoop;
                }

                if (_fsm.StateEvent == StateEvent.StateUpdated)
                {
                    return NodeState.BlockAppending;
                }

                if (_fsm.StateEvent == StateEvent.MiningStart)
                {
                    return NodeState.GeneratingConsensusTx;
                }

                if (_fsm.StateEvent == StateEvent.LongerChainDetected && _caught)
                {
                    return NodeState.Reverting;
                }

                return NodeState.Stay;
            }

            _fsm.AddState(NodeState.ExecutingLoop)
                .SetTransferFunction(TransferFromAddExecutingLoop)
                .OnEntering(WhenEnteringState)
                .OnLeaving(WhenLeavingState);
        }

        private void AddReverting()
        {
            NodeState TransferFromAddReverting()
            {
                if (_fsm.StateEvent == StateEvent.RollbackFinished)
                {
                    _caught = false;
                    return NodeState.Catching;
                }

                return NodeState.Stay;
            }

            _fsm.AddState(NodeState.Reverting)
                .SetTransferFunction(TransferFromAddReverting)
                .OnEntering(WhenEnteringState)
                .OnLeaving(WhenLeavingState);
        }

        private void WhenEnteringState()
        {
            Logger.LogTrace($"[NodeState] Entering State {_fsm.CurrentState.ToString()}");
            MessageHub.Instance.Publish(new EnteringState(_fsm.CurrentState));
            
            if (_inAState == 1)
            {
                Logger.LogTrace("Unexpected entering of current state.");
            }
            Interlocked.Add(ref _inAState, 1);
        }

        private void WhenLeavingState()
        {
            Logger.LogTrace($"[NodeState] Leaving State {_fsm.CurrentState.ToString()}");
            MessageHub.Instance.Publish(new LeavingState(_fsm.CurrentState));
            
            if (_inAState == 0)
            {
                Logger.LogTrace("Unexpected leaving of current state.");
            }
            Interlocked.Add(ref _inAState, 0);
        }
    }
}