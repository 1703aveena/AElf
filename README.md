# AElf - A Decentralized Cloud Computing Blockchain Network 

[![GitHub closed issues](https://img.shields.io/github/issues-closed/aelfproject/aelf.svg)](https://app.gitkraken.com/glo/board/XKsOZJarBgAPseno)
[![lisence](https://img.shields.io/github/license/AElfProject/AElf.svg)](https://github.com/AElfProject/AElf/blob/dev/LICENSE)
[![Nuget](https://img.shields.io/nuget/v/AElf.OS.svg)](https://www.nuget.org/packages?q=aelf)
[![MyGet (with prereleases)](https://img.shields.io/myget/aelf-project-dev/vpre/aelf.os.svg?label=myget)](https://www.myget.org/gallery/aelf-project-dev)
[![Twitter Follow](https://img.shields.io/twitter/follow/aelfblockchain.svg?label=%40aelfblockchain&style=social)](https://twitter.com/aelfblockchain)
[![Gitter](https://badges.gitter.im/aelfproject/community.svg)](https://gitter.im/aelfproject/community?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge)

BRANCH | TRAVIS CI | APPVEYOR | CODE COVERAGE
-------|-----------|----------|--------------
MASTER |[![Build Status](https://travis-ci.org/AElfProject/AElf.svg?branch=master)](https://travis-ci.org/AElfProject/AElf) | [![Build status](https://ci.appveyor.com/api/projects/status/wnehtmk2up4l5w5j?svg=true)](https://ci.appveyor.com/project/AElfProject/aelf/branch/master) | [![codecov](https://codecov.io/gh/AElfProject/AElf/branch/master/graph/badge.svg)](https://codecov.io/gh/AElfProject/AElf)
DEV    |[![Build Status](https://travis-ci.org/AElfProject/AElf.svg?branch=dev)](https://travis-ci.org/AElfProject/AElf) | [![Build status](https://ci.appveyor.com/api/projects/status/wnehtmk2up4l5w5j/branch/dev?svg=true)](https://ci.appveyor.com/project/AElfProject/aelf/branch/dev) | [![codecov](https://codecov.io/gh/AElfProject/AElf/branch/dev/graph/badge.svg)](https://codecov.io/gh/AElfProject/AElf)

Welcome to AElf's official GitHub repo ! 

AElf is a blockchain system aiming to achieve scalability and extensibility through the use of side-chains and a flexible design. To support multiple use cases AElf makes it as easy as possible to extend/customize the system by providing easy to use tools and frameworks in order to customize the chains and write smart contracts. AElf will eventually support various languages that will let developers choose the one they are the most comfortable with.

For more information you can follow these links:
* [Official website](https://aelf.io)
* [Documentation](https://docs.aelf.io/v/dev/)
    * [Environment setup](https://docs.aelf.io/v/dev/main/main/setup)
    * [Running a node](https://docs.aelf.io/v/dev/main/main/run-node)
    * [Smart contract development](https://docs.aelf.io/v/dev/main/main-1)
    * [Web Api](https://docs.aelf.io/v/dev/reference)
    * [Testnet](https://docs.aelf.io/v/dev/resources/testnet)
* [White Paper](https://grid.hoopox.com/aelf_whitepaper_EN.pdf?v=1) 

This repository contains the code that runs an AElf node, you'll find bellow other important repositories in the AElf 
ecosystem:

TOOL/LIBRARY | description
-------------|-------------
[aelf-sdk.js](https://github.com/AElfProject/aelf-sdk.js) | a Javascript development kit for interacting with an AElf node, useful for dApp developers. 
[aelf-command](https://github.com/AElfProject/aelf-command) | a CLI tool for interacting with an AElf node and wallet.
[AElf-boilerplate](https://github.com/AElfProject/aelf-boilerplate) | a framework for smart contract and dApp development.

## Getting Started

### This repository

This repo is where you will find the code that can use to run an AElf node. It also contains a **tests** folder that centralizes all the unit tests.

### Prerequisites

We strongly recommend you follow official documentation that will guide you through installing dependencies and running the node, 
these two guides will get you started:  
* [Environment setup](https://docs.aelf.io/v/dev/main/main/setup)  
* [Running a node](https://docs.aelf.io/v/dev/main/main/run-node)  

## Node configuration

To configure the nodes behavior, many options are available to the operator (some encountered in the [Running a node](https://docs.aelf.io/v/dev/main/main/run-node) tutorial). The options are located here [appsettings template](https://github.com/AElfProject/AElf/blob/dev/src/AElf.Launcher/appsettings.json).

Here's a list of some of the most important:

#### Network

``` JSON
  "Network": {
    "BootNodes": ["127.0.0.1:6801", "127.0.0.1:6802"], 
    "ListeningPort": 6800 
  },
```

- BootNodes: list of nodes that the node will try to connect to.
- ListeningPort: port on which the node will listen for incoming P2P connections.

#### Web API endpoint

``` JSON
"Kestrel": {
    "EndPoints": {
        "Http": {
            "Url": "http://*:1234/"
        }
    }
},
```

- Url: the listening IP and port of the Web API ('*' for listening on all interfaces).

## Web API

The node comes with an API from which you can query useful information about the node and the blockchain.

An easy way to discover the available methods is through the API's swagger interface 

```http://localhost:1234/swagger/index.html```
(replace with the ```ip:port``` of your node's web api)

A more complete reference can be found here [Web Api Reference](https://docs.aelf.io/v/dev/reference).

## Contributing

If you have a reasonable understanding of blockchain technology and at least some notions of C# you can of course contribute. We also appreciate other types of contributions such as documentation improvements or even correcting typos in the code if you spot any.

We expect every contributor to be respectful and constructive so that everyone has a positive experience, you can find out more in our [code of conduct](https://github.com/AElfProject/AElf/blob/dev/CODE_OF_CONDUCT.md).

### Reporting an issue

We currently only use GitHub for tracking issues, feature request and pull requests. If you're not familiar with these tools have a look at the [GitHub](https://help.github.com/en) documentation.

#### Bug report

If you think you have found a bug in our system feel free to open a GitHub issue, but first:
- check with GitHub's search engine that the bug doesn't already exist.
- in the request give as much information as possible such as: the OS, the version of AElf, how to reproduce...

#### Missing feature

We also use the GitHub issue tracker for features. If you think that some piece of functionality is missing in AElf, you can open an issue with the following in mind:
- check for similare feature requests already open.
- provide as much detail and context as possible.
- be as convincing as possible as to why we need this feature and how everybody can benefit from it.

### Pull request

For any non trivial modification of the code, the pull requests should be associated with an issue that was previously discussed. During the time you implement and are not yet ready for review, prefix the PR's title with ```[WIP]``` and also don't forget to do the following:
- add a description in the pull request saying which issue you are fixing/implementing. 
- be as explicit as possible about the changes in the description.
- add the tests corresponding to your modifications.
- pull requests should be made against the **dev** branch.

When you are ready for a review by the core team, just remove ```[WIP]``` from your PR's title and others will review. This will either lead to a discussion or to a refactor of the code. The Travis CI system makes sure that every pull request is built for Windows, Linux, and macOS, and that unit tests are run automatically. The CI passing is a pre-condition for the PR to be merged as well as the approval from the core team.

## Versioning

We use Semantic Versioning (SemVer) for versioning, if you're intereted in closely following AElf's developement please check out the [SemVer docs](https://semver.org/).

## License

AElf is licenced under [MIT](https://github.com/AElfProject/AElf/blob/dev/LICENSE)
