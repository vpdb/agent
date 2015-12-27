VPDB Agent Features
===================

### Files

- Watch `PinballX.ini` for changes. If the file changes, re-parse it and update UI if shown.
- Watch XML files in PinballX's database folders for changes. If any file is changed, added or deleted, update the internal database and UI if shown.
- Watch table files. If files are renamed or deleted, update internal database. If files are added, check missing entries against added files.
- Be able to filter by "systems" and files


### Data

- Receive realtime data through Pusher notifications. If a release is starred at vpdb.io, immediately start downloading (if setting is enabled).
- If a release is updated at VPDB, immediately download update.
- Refresh data on application startup. If a release was starred while VPDB Agent wasn't running, update UI accordingly and download release if setting is enabled.


### Downloads

- Be able to identify previously downloaded tables among releases at vpdb.io. This makes it possible to get future updates for tables downloaded through other channels like VPF without having to re-download the table.
  - When file name and size of the local file are identical to a file at vpdb.io, assume a match and link directly.
  - When the size is within a threshold, display a list of matches for the user to confirm.
  - Try to match all local files with one click
- When downloading a release, also check if media and ROMs are locally available and download if available on vpdb.io. Currently supported media:
  
  - Table images
  - Table videos
  - Backglass images (currently only 5:4 without grill)
  - Wheel images
  - ROMs
- Only download `N` (currently 2) files at the same time
- Show download status of all current, future and past files in the app's *Downloads* tab
- Be able to remove items from the downloads tab
- Be able to clear all processed items from the downloads tab with one button
- Apply script changes to updates: When updating a release, apply a diff from the user's script changes to the script of the updated release.
- Automatically choose correct flavor based on user's settings (see FAQ)


### Notifications

- Display a notification when something "interesting" happens (and setting is enabled) and add an entry to the log list.
- Always display a notification when something failed and user interaction is necessary.


### Application

- Clean, modern look
- Install without requiring admin rights
- Update automatically
