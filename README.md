# VPDB Agent

*A Windows desktop application that syncs local data with VPDB*.

## What is it?

A program that runs in the background on your cab and deals with all the love
coming from VPDB. It also comes with a nice UI for configuring it.


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
- Try pusher.com for instant downloads after starring if enabled
- Watch folders so data gets updated when files do
- Be pessimistic on shit, i.e. don't crash if something unexpected happens


## Tech Stack / Dependencies

- Microsoft .NET 4.5 WPF Application
- [Refit](https://github.com/paulcbetts/refit) for type-safe REST access
- [Mahapps.Metro](http://mahapps.com/) for easy custom styling
- [INI File Parser](https://github.com/rickyah/ini-parser) for parsing PinballX config


## License

GPLv2, see [LICENSE](LICENSE).