# Dropbox Shared Link Clone
A simple Windows utility to clone the contents of a [Dropbox](https://dropbox.com) 'shared link' to a local folder

## Purpose
The [Dropbox desktop app](https://dropbox.com/install) is a great tool to synchronize your Dropbox folders on the cloud with a local directory but its support is limited to your own files and folders.

If another user has shared a link to their files or folders, all the desktop app will do is provide you a clickable link to that shared folder and you have to manually view and download files from a web page.

Worse than that, the view of those files is not flexible - for example you cannot alter the way the collection of files are sorted and you can only download either a single file, or an entire folder.

The purpose of this utility is to automatically download the contents of a shared link folder with a folder on your system, but only download the files not previously downloaded.

Note that this is **NOT** a two-way sync, it is just a way to maintain a copy the files locally so that they can then be viewed in the Windows File Explorer, using all of the File Explorer view and sorting options.

## Usage
Dropbox, understandably, requires a few security measures to control access to data on the cloud and so this utility needs some information before it can operate. This information is stored locally so that it doesn't have to be re-entered.

On first launch, you will be required to type in an AppKey and AppSecret. This will have to be new and unique to you and obtained by creating a new app at the [Dropbox developer's app console](https://www.dropbox.com/developers/apps).

Then, the system will prompt you to log on to DropBox and 'allow' your newly created app to connect to the DropBox ecosystem. A browser window will appear during this process, but only the first time this stuff is set up.

Finally, you will be prompted, in the command window, to enter the shared link URL. The system will allow you to enter multiple URL and each will be cloned.

Finally, the utility will now read the metadata and files from the shared link URL, create an equivalent folder in My Documents, and download the files.

When next launched, the utility will probably not need to prompt for anything, but will simply update the local folder(s) with any new or changed files in the shared link folder.

## Known issues and limitations

* This is a console application. It would be far better as a GUI application and a future version may be based around Windows Forms.
* This is partly based on some Dropbox sample code, and so it may not be the most efficiently implementation of the features I have chosen to add.
* There should be a better way to control the list of shared link folders to use. Currently, there is no simple way to maintain this list

## Change log
### 1.0.1.0
* Reduced the amount of textual output during operation of the utility
* Added command line options for the following features:
    * -h, --help<br>Display help information
    * -v, --verbose<br>Display verbose progress information
    * -np, --no-prompt<br>Do not wait for a keypress before exit
    * -ra, --reset-all<br>Delete all configuration and prompt for it to be entered again
    * -rsl, --reset-shared-links<br>Delete all shared link folder configuration and prompt for it to be entered again

### 1.0.0.4
* First version considered usable on a clean machine. Install the executable and follow the [Usage](#usage) instructions.
