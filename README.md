## [**Download Latest Release**](https://github.com/Yen/PSRT.Astra/releases/latest)

# PSRT.Astra

[![Build Status](https://dev.azure.com/PSRT/PSRT.Astra/_apis/build/status/Yen.PSRT.Astra)](https://dev.azure.com/PSRT/PSRT.Astra/_build/latest?definitionId=1)
[![Download Now](https://img.shields.io/github/downloads/Yen/PSRT.Astra/total.svg?style=popout)](https://github.com/Yen/PSRT.Astra/releases/latest)

![PSRT.Astra](https://i.imgur.com/hkFRNS8.png)

# Why?

## Speed and validity

Astra was built as a reponse to issues with installation corruption in the PSO2 Tweaker. The main issue stemmed from the fact that the Tweaker did not perform file checks on launch, only when requested. This meant if your installation was corrupted somehow it would not be detected until your game crashed. This also had the problem that if your game _was_ corrupted your only option was to sit through a 30 minute slow file check, even if the only corruption was a single 1 or 0 being out of place.

**Astra solves this.**

Astra uses a complex caching and record keeping technique so that an entire file check can be done in a matter of seconds. As such, this operation is run automatically each time the application is launched! You can try this out for yourself: Go into the pso2 game files, delete a random file, start Astra. You will see that with surgical precision Astra is able to locate the missing game file, patch just that file, and be ready to play. All with in a matter of seconds!

Now this method does not come without issues of course. It is techincally possible to get a corrupted installation if a program manually modifies the last write times of the file system entities to perfectly match what is in the database but with different data in the file... But this is a very _rare_ edge case that I don't know of a single way it could come about except for intentionally trying to corrupt your installation. Even if this was to happen however, the solution would be to perfom an old style "full scan", so it's always _as_ good as the current method, but better 99% of the time.

A key part that helps Astra keep on top of things in regards to your game state is that Astra trusts nobody but itself. Each time the application starts Astra assumes that every file in your PSO2 installation is corrupt until it has proven to itself its not. The only thing it trusts is it's own database file, which should not get modified by any other application. As such, this file can be trusted to be in a valid state.

This "no trust" design means you can run Astra on any installation of PSO2 you like, downloaded by the official client or downloaded by the PSO2 Tweaker. Astra might have to take some time to learn about your installation on the first run, but after that no matter what you do to it, Astra will always be able to get your installation into a running state and should never let you launch a corrupt or outdated game.

## Legacy 

A second factor for why Astra was written was that the PSO2 Tweaker is old now. It is by no means bad software but it suffers from decisions made years ago. Astra was written from the ground up using modern technologies and algorithms meaning that going forwards it should be easier to implement and extend new features.

Astra being new also means there is _considerably_ less code behind it, this means less code that could be broken, hopefully leading to a more stable application.

## Design

Astra was built with a basic user premise in mind:
1. Run program
2. Press play

A user should not have to deal with anything else. If they have gotten to the point where they can click the play button, then their game should work how they want. As such almost all options that are presented to the user are optional and should not require the user to worry about them once they have decided.

# Arks Layer

The Arks Layer team has very kindly allowed us to utilise their tools as Astra does not attempt to replicate the functionality of any of these, just the Tweaker itself. As such, Astra ships with support the the English patch and the Telepipe Proxy. It should be noted that Arks Layer does not allow the use of their tools without request! If you are considering using the code here to replicate functionality be warned that you **must** request access from them first. The user agent used by Astra was provided _for_ the use by Astra only.