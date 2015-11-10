# VPDB Agent

*A Windows desktop application that syncs local data with VPDB*.

## What is it?

A program that runs in the background on your cab and fills the gap between
[PinballX](http://pinballx.net) and [VPDB](https://github.com/freezy/node-vpdb).

It also comes with a nice UI for configuring and monitoring it. Oh, and it's
completely real-time, meaning if you change stuff locally or on VPDB, 
VPDB Agent will know immediately and react.

## Features

The [MVP](https://en.wikipedia.org/wiki/Minimum_viable_product) should have the
following features:

- Match local games against releases at VPDB
- Update local games automatically from VPDB where enabled
- Download starred games automatically from VPDB if enabled

From there, we can imaginate more features such as media management, browsing,
rating and so forth.


## Implementation

The above implicates:

- Initial setup where user is asked for PinballX folder and VPDB API key
- Advanced setup for VPDB end point and basic auth
- Filtering by system while listing games would be useful
- Try matching by file size and see how it turns out
- Use pusher.com for realtime sync between VPDB
- Watch folders so data gets updated when files do
- Be pessimistic on shit, i.e. don't crash if something unexpected happens


## Tech Stack / Dependencies

- Microsoft .NET 4.5 WPF Application
- Asynchronous backend with [Rx Extensions](https://rx.codeplex.com/)
- UI wiring using [ReactiveUI](http://reactiveui.net/)
- [Refit](https://github.com/paulcbetts/refit) for type-safe REST access
- [Mahapps.Metro](http://mahapps.com/) for easy custom styling
- [INI File Parser](https://github.com/rickyah/ini-parser) for parsing PinballX config
- [Humanizer](https://github.com/MehdiK/Humanizer) for all kind of formatting tricks
- [NLog](http://nlog-project.org/) for our logging needs


## Database

The goal is to touch current XML files that make the PinballX database only
when necessary (i.e. a release is updated). Instead, we keep a `vpdb.json` in
each folder that contains the additional data we need to work with. This file
basically serves as our local database is updated as the XMLs are changed 
manually or by another application.

As for adding new games, we'll be using a separate XML that sits besides the
current ones.

## Packaging

We're using [Squirrel](https://github.com/Squirrel/Squirrel.Windows) for 
packaging the builds. In order to release a new version:

1. Bump version in `AssemblyInfo.cs` AND `vpdb-agent.nuspec`
2. **Build Release**
3. In the *Package Manager Console*, type: 

   ```
   PM> nuget pack .\vpdb-agent.nuspec
   PM> squirrel --releasify .\VpdbAgent.0.0.1.nupkg
   ```
5. Commit, tag and push
4. Package `setup.exe` as `vpdb-agent-0.0.1.zip`
6. Create release on GitHub and attach zip
7. Bump version

## License

GPLv2, see [LICENSE](LICENSE).