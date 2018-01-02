# Thinking in Interoperablity of AELF

When we talk about interoperability of blockchain, the first question we have to answer is : 

> "How the transaction on one blockchain can trigger the execution of a smart contract in the another blockchain?"

In the simplest form between Ethereum and Bitcoin, we can write a piece of code to monitor the events on Ethereum with proper libraries,
such as [web3js](https://github.com/ethereum/web3.js/) or [JSONRPC](https://github.com/ethereum/wiki/wiki/JSON-RPC), for example,
if a given smart contract has been called and verified as valid, and if this transaction 
has been confirmed for more than 12 or 36 times on its block-height, the monitor program can safely trigger the predefined actions 
on the bitcoin blockchain by creating a signed [raw transaction](https://en.bitcoin.it/wiki/Raw_Transactions) and broadcast
it to the bitcoin network. 

One advantage of this centralized way for blockchain interoperability is its simplicity. Basically, we can write as many adapters
as we want to connect with different blockchains, broadcast the transactions to any one of them, and even trigger cascaded actions, 
e.g. if I deposit some ethers to one specific smart contract on Ethereum. The monitor can trigger the actions on Bitcoin and to pay
the bill with [Lighting Network](https://lightning.network/) to some service provider and the vendor finally triggers a movement or action on a tiny cute
android connected to IOTA network. Yes,  it looks that the '**adapter way**' eliminates the boundaries of different blockchains
and works like a charm, except one thing, It is centralized.

Let’s think twice on the critical assumption in the scenario above. We laid our trust on the centralized monitor, the middleman 
precisely. We trust this middleman for his guaranteed behaviour to happen on the other blockchains. But what if he didn’t keep
his promise, what if a link in the chain of blockchains has broken, or we can say: this process is not '**atomic**' from technique 
perspective.

In AELF, making the whole process automatic, guaranteed and decentralized is our goal.

If the counterparties in different blockchains wants to exchange assets, meanwhile they don’t trust each other, there have to be
a middleman they all trust, think about how you buy and sell assets on a centralized exchanges, e.g. Bittrex, we somewhat laid trust
on these exchanges intentionally or unconsciously.

Decentralized exchanges(DEX) are becoming epidemic nowadays, projects like 0x, Kyber Network and AirSwap won great attention by 
the public. General speaking, DEX is a special use case in cross-chain interoperability. The idea behind DEX is called '**atomic swap**', 
i.e. we swap assets without third parties, and provide ‘end-to-end’ security in token exchanges, or we hope to be at least.

The '**mainchain**' endorsement mechanism we are going to set up in AELF is a decentralized middleman (DPoS), who can provide '**trust**' 
for each side-chain in AELF ecology. All side chains in AELF ecology can lay their trust on the '**mainchain**' for cross-chain
transactions. It works as if one sidechain can provide a proof that can be verified through the mainchain, then the sidechains 
other than the one are convinced to believe that the corresponding transaction has happened indeed. The whole process of a 
cross-chain transactions can be formalized as below:

1. A transaction happens on the sidechain A.
2. Sidechain A sends a message to the ‘mainchain’ in order to record this transaction with minimal required information on the '**mainchain**', i.e. the merkle root simply. 
3. Sidechain A actively makes a function call to the corresponding method from another sidechain B with a proof — i.e. the merkle proof. 
4. The method in sidechain B tries to verify the proof on the '**mainchain**' that both side-chain trust, and execute the corresponding actions if verified valid.

As we can see from the steps above, this is actually an '**active**' process. If we trust the smart contracts (open source)
on both sidechains, we can confirm that the above procedure will definitely run as expected, automatically and controlled 
by the one who initiated the transaction.

As for '**passive**' monitoring paradigm like the scenario at the beginning of this article, we may construct differently, like below:

1. Sidechain B monitors transactions on side-chain A.
2. If a special transaction has happened on sidechain A, to make sure it’s not deniable, side-chain B waits until the 'merkle root' of side-chain A has been included in the '**mainchain**'.
3. After it’s confirmed on the '**mainchain**' and verified as valid, Side-chain B executes its corresponding actions.

As we can see from above, merkle proof has played an important part in both '**active**' and '**passive**' cross-chain transactions,
let’s talk a little more about the merkle proof:

In cryptography, A '**merkle proof**' is a kind of signature proof which can be used to prove that a given transaction exists 
in the target block, a merkle proof contains:

(merkleroot of the block, hash1, hash2,…. hashN)

Basically, it’s a list of hashes as an evidence taken from the merkle tree to prove the existence of a transaction. 

The security of merkle proof relies upon the computational infeasibility of hash collision, i.e. you can barely fake 
a meaningful transaction whose transaction hash can be computed the same as the previous one. Generally speaking, once t
he merkle root in a sidechain has been included in the mainchain, we can conclude that all the transactions in 
that block has been confirmed by the '**mainchain**', meanwhile the mainchain is kept decentralized with DPoS.
