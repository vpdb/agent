# VPDB Agent

*A Windows desktop application that syncs local data with VPDB*.

[![Build status](https://ci.appveyor.com/api/projects/status/28oc0qo7in6ps680?svg=true)](https://ci.appveyor.com/project/freezy/vpdb-agent)
[![Coverage Status](https://coveralls.io/repos/freezy/vpdb-agent/badge.svg?branch=master&service=github)](https://coveralls.io/github/freezy/vpdb-agent?branch=master)

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

For more details, see [FEATURES](FEATURES.md).


## Tech Stack / Dependencies

- Microsoft .NET 4.5 WPF Application
- Asynchronous backend with [Rx Extensions](https://rx.codeplex.com/)
- UI wiring using [ReactiveUI](http://reactiveui.net/)
- [Refit](https://github.com/paulcbetts/refit) for type-safe REST access
- [Mahapps.Metro](http://mahapps.com/) for easy custom styling
- [INI File Parser](https://github.com/rickyah/ini-parser) for parsing PinballX config
- [Humanizer](https://github.com/MehdiK/Humanizer) for all kind of formatting tricks
- [NLog](http://nlog-project.org/) for our logging needs
- [Akavache](https://github.com/akavache/Akavache) for settings storage and image caching
- [NotifyIcon](http://www.hardcodet.net/wpf-notifyicon) for tray features
- [OpenMcdf](http://sourceforge.net/projects/openmcdf/) for reading and writing VP's OLE Compound format
- [XUnit](https://xunit.github.io/), [Moq](https://github.com/Moq/moq4/wiki/Quickstart) and [Fluent Assertions](https://github.com/dennisdoomen/fluentassertions/wiki) for unit tests


## Tests

Tests are written with [XUnit](https://xunit.github.io/) and 
[Fluent Assertions](https://github.com/dennisdoomen/fluentassertions/wiki).
When using Resharper, use [the plugin](https://resharper-plugins.jetbrains.com/packages/xunitcontrib/)
so you can easily debug. Coverage is still poor but increasing.


## Database

The goal is to touch current XML files that make the PinballX database only
when necessary (i.e. a release is updated). Instead, we keep a `vpdb.json` in
each folder that contains the additional data we need to work with. This file
basically serves as our local database and is updated as the XMLs are changed 
manually or by another application.

As for adding new games, the user can choose between a separate custom list or
the original XML.

## Packaging

We're using [Squirrel](https://github.com/Squirrel/Squirrel.Windows) for 
packaging the builds. In order to release a new version:

1. Bump to release version at `AssemblyInfo.cs` AND `vpdb-agent.nuspec`. Also 
   update release in the NU specs.
2. **Build Release**
3. Add new `.dll`s to `vpdb-agent.nuspec` if necessary.
4. In the *Package Manager Console*, type: 

   ```
   PM> nuget pack .\VpdbAgent\VpdbAgent.nuspec
   PM> squirrel --releasify .\VpdbAgent.0.0.x.nupkg
   ```
5. Run `.\Releases\Setup.exe` and test
6. If all good, commit, tag and push
7. Package `setup.exe` as `vpdb-agent-0.0.x.zip`
8. Create release on GitHub and attach zip
9. Bump to snapshot version.

## Credits

- Error and crash reporting for VPDB Agent has kindly been provided by 
  [Raygun](https://raygun.io/?ref=vpdb-agent). And by the way, it also 
  works with a ton of other platforms!

  <a href="https://raygun.io/?ref=vpdb-agent"><img src="https://raw.githubusercontent.com/freezy/vpdb-agent/master/Assets/raygun.png" width="200"></a>
- @andreaskoepf for some really helpful Rx suggestions

## License

GPLv2, see [LICENSE](LICENSE).