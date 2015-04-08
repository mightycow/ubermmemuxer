# [UMM](https://github.com/mightycow/ubermmemuxer) - Uber MME Muxer

Purpose
-------

UMM is used to simplify the batch processing of video and image sequences that are output by [**Q3MME**](http://q3mme.proboards.com/) (**Quake 3 Movie-Maker Edition**).  
It allows you to mux all of the output into .avi files with audio (when available).

Installation
------------

UMM requires [**.NET Framework 4.0 Client Profile**](http://www.microsoft.com/en-us/download/details.aspx?id=24872) at a minimum to to run.  
If the link doesn't work, seach for **Microsoft .NET Framework 4 Client Profile (Standalone Installer)**.

UMM requires **MEncoder**, which is part of the free and open source **MPlayer** video player package.  
It doesn't need any installation, you can extract it anywhere (even in **Program Files** or **Program Files (x86)**).  
Only the version described as **for 32-bit Windows** will work with **VFW** codecs, so you should grab that one.  
You can find the [Windows builds here](http://mplayerwin.sourceforge.net/downloads.html).

If you want to use a **VFW** codec (like Lagarith, for instance), you have to copy the .dll file(s) to **$(MPlayer_folder)\codecs**.

UMM itself creates a `Config.xml` settings file right next to the executable so it's best to:
1. Have UMM in its own folder, so that `Config.xml` stays with the executable.
2. Not have UMM in **Program Files** or **Program Files (x86)**

Setting up
----------

Open up UMM.exe and navigate to the `Settings` tab.  
Under `General Settings`, make sure you fill in the `MEncoder Path` text box with the file path to your *MEncoder.exe*.

Use Cases
---------

| Input format                                      | File name                 | COP<sup>[1]</sup>: off | COP<sup>[1]</sup>: on | CODEC used | Audio?
|:--------------------------------------------------|:--------------------------|:-------------------|:------------------|:-----------|:------------
| **1)**  Single .avi file                          | $(final_name).avi         | Same   | Custom | Color      | If available
| **2)**  Folder with .avi file(s) + optional .wav  | $(final_name).avi         | Parent | Custom | Color      | If available
| **3)**  Folder with image file(s) + optional .wav | $(final_name).avi         | Parent | Custom | Color      | If available
| **3) a)** if matches (*.depth.*.$(ext))           | $(final_name).depth.avi   | Parent | Custom | Monochrome | No
| **3) b)** if matches (*.stencil.*.$(ext))         | $(final_name).stencil.avi | Parent | Custom | Monochrome | No
1. COP is Custom Output Folder, see `Output all files to this foler` under `General Settings`

All output files (marked as `$(final_name)` above) are named according to the `File Naming Rules` settings.

Settings
--------

| Video CODEC | Description |
|:------------|:------------|
| Raw         | This will actually transform the input into a color-space/codec of MEncoder's choosing. |
| Copy        | This will tell MEncoder to leave the data untouched whenever possible. Note that .tga inputs will create MTGA (Motion TGA) outputs and .png inputs will create MPNG (Motion PNG) outputs. |
| Lagarith    | Use the Lagarith lossless codec: this codec only supports RGB(A) and YUV color spaces. |
| Lavc        | Use a codec supported by the libavcodec library. |

### Monochrome Video CODEC
* The same explanations as for the `Video CODEC` settings above apply, except...
* Those settings are only used for depth/stencil image sequences.
* Depth sequences are those including the string ".depth." in the image file names.
* Stencil sequences are those including the string ".stencil." in the image file names.

### Input and output FPS
If *In* is the `Input Frame Rate` and *Out* the `Output Frame Rate`, we have the following 3 possible scenarios:

| Scenario | What to do or look out for |
|:---------|:---------------------------|
| Out > In | This should never happen, render at the desired frame-rate instead. |
| Out = In | It's all good. |
| Out < In | This is usually used to have shorter *blur traces*. Make sure the input value is an *integer multiple* of the output value: `In = n * Out, where n is an integer` |

Contact
-------

GitHub user: [mightycow](https://github.com/mightycow)  
GitHub project page: [ubermmemuxer](https://github.com/mightycow/ubermmemuxer)

myT @ QuakeNet on IRC  
myT @ [ESR](http://esreality.com/?a=users&user_id=37287)

Thanks
------

The Q3MME developers  
The MPlayer/MEncoder developers  
entik  
farnish  
santile  

License
-------

The entire source code in this release is covered by the GPL.  
See [COPYING.txt](@TODO:) for the GNU GENERAL PUBLIC LICENSE.

Uber MME Muxer (UMM) is Copyright (C) 2013-2015 Gian 'myT' Schellenbaum.
