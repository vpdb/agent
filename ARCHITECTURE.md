# VPDB Agent Data Model

When listing games, VPDB Agent deals with data that comes from three sources, namely:

1. The file system (vpt/vpx files in your table folders)
2. The PinballX Database (xml files in the `database` folder)
3. VPDB

Additionally, there is data linking local files to VPDB release files among other attributes that we call **Mappings**.

These three sources plus the Mappings are the what's shown as games in the main screen of the application. We call them **Global Games**. They are a memory-only reactive list with the only goal of displaying and filtering games in the UI.

The main part of this document deals with how Global Games are aggregated. Other data is briefly discussed at the end.

## File System

The goal of VPDB Agent being used for managing virtual pinball game files, it needs to be aware of which files are available locally. Therefore, all folders configured in `PinballX.ini` are observed. That means that every table file in these folders will create an entry in Global Games.

Note that it's possible to mark files as ignored and that multiple versions of the same release of a game can be grouped. More on that below.

## PinballX Database

This database originates from XML files in PinballX's `database` folder. It's what finally shows up in PinballX's interface. It contains data such as this:

```xml
<menu>
	<game name="Theatre of magic VPX NZ-TT 1.0">
		<description>Theatre of Magic (Bally 1995)</description>
		<manufacturer>Bally</manufacturer>
		<year>1995</year>
		<rating>10</rating>
	</game>
</menu>
```

Like table files in the previous section, every entry in this database results in an item in Global Games. If the file is physically available, it'll be the same entry, otherwise a new entry is created.

VPDB Agent can read and write PinballX' database files.

### PinballX Configuration

Obviously we wouldn't know where to look for PinballX database and table files if it weren't for `PinballX.ini`. This means if the configuration changes, the data changes as well. PinballX has what it calls "Systems". A system is basically an executable that can somehow run a pinball game. For example, the Visual Pinball system looks like this:

```ini
[VisualPinball]
Enabled=true
WorkingPath=C:\Pinball\Visual Pinball
TablePath=C:\Pinball\Visual Pinball\Tables
Executable=VPinball991.exe
```

VPDB Agent is watching the configuration file for changes and updates the data immediately. The configuration file is only read, never written.

## VPDB

VPDB Agent displays data from VPDB such as release names, authors, dates and thumbnails. Data is fetched from VPDB when identifying a release and saved in a local database based on LiteDB. The data is the same as received from VPDB and therefore acts as a cache.

Data from this cache is only used to display richer content in the UI, i.e. Games in the cache won't show up in Global Games unless there exists a Mapping for it (see next section).

When VPDB Agent is running, updates from VPDB are pushed directly to the client. When starting up, VPDB Agent checks for updates.

## Mapping

When linking local data to VPDB, VPDB Agent needs a way of persisting that mapping in a transparent way. For that, it creates a `vpdb.json` file in every system folder. Such a file looks something like this:

```json
{
  "mappings": [
    {
      "id": "Theatre of Magic (Bally 1995)",
      "filename": "Theatre of magic VPX NZ-TT 1.0.vpx",
      "database_file": "Visual Pinball.xml",
      "release_id": "e2wm7hdp9b",
      "file_id": "skkj298nr8",
      "is_enabled": true,
      "is_synced": false
    }
  ]
}
```

There are a few more attributes like `previous_file_ids`, which is used to group multiple versions of the same file. Another property `patched_table_script` is used to applying diffs of table script changes.

It's also possible to use a Mapping for ignoring local unimportant files. In this case, the mapping doesn't contain `id` or VPDB IDs, just `filename` with an `is_hidden` property, for example:

```json
{
  "mappings": [
    {
      "filename": "default table.vpx",
      "is_hidden": true
    }
  ]
}
```

Every Mapping with an ID creates an entry in Global Games. If an entry for the given ID or filename already exists, it is updated and enriched with data from VPDB. It's possible that a game is listed in VPDB Agent that has neither a local file nor a PinballX database entry if that game was previously mapped to a local file which has been removed since.

## Aggregation of Global Games

As described so far, Global Games gets updated in either of these cases:

- The PinballX configuration is updated
- An XML database from PinballX is updated, created or removed
- A new vpt/vpx file is created, renamed or removed in one of the monitored table folders
- A `vpdb.json` mapping file is updated

Updates from VPDB Agent are never directly pushed to Global Games. Instead, changes are applied at its source, resulting in Global Games being updated automatically. For example, if the user adds a table file to a PinballX database, VPDB Agent updates the XML first, which will then result in Global Games being updated as well.

This means that Global Games don't contain any game-related data which can't be reconstructed through the file system, the Mapping files, PinballX's database file, or VPDB.

### Data Flow


#### PinballX

It all starts with `PinballX.ini`. On any changes and on application start-up, it's parsed and a list of systems is created. On start-up, all systems are initialized, on changes only new systems are initialized, which results in a Observable of every system's `Enabled` attribute.

This Observable fires initially and in the future when `PinballX.ini` is updated and the `enabled` status changes. If the value is `true`, games from the XML database are parsed and watched for further changes, otherwise watchers are destroyed and games removed.

This is where the Game Manager comes in, which manages the Global Games. It subscribes to a `GamesUpdated` Observable, which fires every time an XML database file from PinballX is updated, added, created or renamed. The Game Manager then checks which data needs to be updated and only touches concerned objects. Since Global Games is a reactive list containing reactive objects, changes are immediately relayed to the UI.

Global Games is a flat structure. While systems also contain lists of their proper games, this nested structure serves only for internal usage, for example for knowning which objects to remove in case an XML database file gets deletd.

#### File System

Since table file locations are defined `PinballX.ini`, the file system watcher subscribes to the parsed systems as well, but not only to the `Enabled` property but also to the `TablePath` property. Since table paths can overlap, it keeps a decoupled global list of folders to watch.


## Other Data

VPDB Agent doesn't just list games but also deals with downloads, has log messages and probably more data in the future. All this data is saved in the same LiteDB as the VPDB cache.
