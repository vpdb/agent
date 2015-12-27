VPDB Agent FAQ
==============

### What's the point of VPDB Agent?

While vpdb.io makes searching and browsing very comfortable, you still need to extract everything you download to the right place and update PinballX's database file. But most importantly, you physically need to do it on your virtual cabinet (unless you have some clever file sharing going on), which is probably not equipped with a comfy chair like the one you're sitting in right now. And sometimes, updating tables can take a while.

VPDB Agent does all this for you. It sits on your cab and if you star a release on vpdb.io, it'll download it, put it at the right place and update PinballX's database. It'll also download all the other stuff if missing, like ROMs, background vids, table shots, game logos and so on. Additionally, it keeps up with new versions of that release. All that in one single click!


### What happens if I update my XML files of the PinballX database while VPDB Agent is running?

VPDB Agent is watching relevant files for changes, so it'll update itself as soon as you change your files. Same thing for `PinballX.ini` or the files you have in your tables folder.


### Does VPDB Agent work with Hyperpin as well?

Not yet but probably will at some point.


### How does VPDB Agent update the XML files?

By default, VPDB Agent tries to be as unobtrusive as possible, meaning it will append a new game at the end of your XML file without touching the rest. It is also clever enough to figure out by how many spaces or tabs your XML is indented, so the new data blends in nicely.

However, if you enable the "Reformat my XML" option, the whole XML file will be re-serialized and reformatted and comments will be stripped off. This is a more stable option, since the entire XML is handled by the serializer, whereas otherwise data is treated by concatenating strings manually in the code.


### What's the difference between "Sync" and "Star"?

Starring is a feature at vpdb.io. It puts a marker on a release or game, increasing its popularity and giving the user a way to filter by it. Semantically, it's a "like" button.

Since we want to remote control VPDB Agent through vpdb.io, we're adding a new semantic to the star: 

> Keep that one in sync!

As soon as you star something at vpdb.io, VPDB Agent will know about it and download it (unless it already has, of course). "Syncing" and "starring" are effectively the same thing.

But you might not want to do that. You might want to keep the starring business at vpdb.io and decide case by case which games you want to sync with VPDB Agent. In this case, you uncheck the "Sync starred releases" option in the settings and VPDB Agent will only sync where you explicitly enable the "sync" toggle.


### How long do I have to wait for VPDB Agent to download the table once I've starred it on vpdb.io?

Glad you asked! We're using push notifications through pusher.com, so it'll be instantaneously.


### How does VPDB Agent know which flavor* to download?

**A flavor is a combination of orientation and lighting settings.*

At vpdb.io, a release can have multiple flavors and if you star a release to be downloaded, you don't indicate which flavor. VPDB Agent figures out which flavor based on the settings:

    If available, please download releases in the following flavor:

    	Orientation should be: [FS, WS, Universal]
    	Lighting should be:    [Day, Night, Universal]

    Otherwise, fall back to the following:

        Orientation must be: [Same, FS, WS, Any]
        Lighting must be:    [Same, Day, Night, Any]

    Where *Same* means "same as previous file if it's an update or same as first choice for new files".


### I've already downloaded games from vpdb.io and I'm installing VPDB Agent just now. Do I need to re-download everything so it gets recognized?

Usually not. Try to "identify" your games, and VPDB Agent will try to find them on vpdb.io.


### What about games from VPF?

Same. If it's a game that is also on vpdb.io, it will probably get recognized.


### So how does this "identification" work?

VPDB Agent searches by file size with a small treshold. If file size and file name are equal to a file at vpdb.io, it's an instant match, otherwise you can choose between results manually.

We'll see how well that works. There are other less trivial ways, like check-summing parts of the table and matching against that.


### I've already starred a ton of games and I'm running VPDB Agent for the first time. Will it download all starred games when I start it?

No, only if you explicitly tell it do so. What you usually do when you start VPDB Agent for the first time is identifying your local games at vpdb.io so nothing gets downloaded multiple times. Then, if you enable the setting "Check for new and updated games on startup", it will download starred games not found locally, or there's also a button that does it so you don't have to restart VPDB Agent in order to do it.


### I usually do subtle changes in my table scripts, like changing the ROM, controller or other settings. When VPDB Agent automatically updates a table, I'll need to apply these changes again, right?

Usually not, and this is really an awesome feature: When updating a table, VPDB Agent creates a [diff](https://en.wikipedia.org/wiki/Diff_utility) between the script of your outdated table and the original script of the outdated table and applies it to the updated table script.

This means that even if the author changed the script in the updated version, your changes will still be applied (unless the author's changes were in the same part of the code, and in this case you'll be notified about it).

In the future, other (not script related) settings such as day/night slider position, playfied friction and more could be part of this process as well.


### Does VPDB Agent run on Windows XP?

About that. See, we feel your pain. You've set up your cab when Hyperpin was the only frontend around and it was painfully slow on Windows 7, so you settled with XP although it was already shut down by Microsoft. Then, PinballX came but you still didn't come around upgrading.

But hey, now you have another reason to upgrade: VPDB Agent is based on .NET 4.5, which simply doesn't run on XP. And not only that, most of the dependencies VPDB uses don't work on XP either. So there's unfortunately no easy way around this. (And you really shouldn't be using XP anyway if you are connected to the internet.)


### Will VPDB Agent ever run on Windows XP?

Probably not. But VPDB Agent is open source, so anyone with enough motivation could have a try porting it back. Pull requests are welcome!


### Why did you make VPDB Agent open source?

Mostly out of ideological reasons. I trust open source software more because I can read the code if I want to. I believe it creates more innovation because everybody can contribute ideas or code I would never have come up with. And it's a way of receiving bug fixes and features from other people who wouldn't have been able to do so for closed source.
