# Election System

## Data Structure

```Protobuf
message Candidates {
    repeated Address Nodes = 1;
}

message Tickets {
    uint64 RemainingTickets = 1;
    repeated VotingRecord VotingRecord = 2;
}
```

In `TokenContract`, we maintain a filed of `Candidates` to simply record all the candidates (who announuced election).

In `ConsensusContract`:

We use a filed of `Candidates` also record all the candidates, and the filed should only be updated by the calling of `AnnouceElection` from `TokenContract`.

Mainly, we use a map of `Address` to `Tickets` to maintain all the balances of addresses.

## For candidates

### Announce election
Send transaction `AnnouceElection` to `TokenContract`;

### Quit election
Send transaction `QuitElection` to `ConsensusContract`

## For voters

### Get tickets
Send transaction `GetTickets` to `TokenContract`

### Vote
Send transaction `Vote` to `ConsensusContract`

### Regret
Send transaction `Regret` to `ConsensusContract`

### Withdraw (give up tickets to get ELFs back)
Send transaction `Withdraw` to `ConsensusContract`

- Can only withdraw remaining tickets, which means if anyone want to withdraw all tickets, first regret his votings.
