======
Author
======

Gian 'myT' Schellenbaum

=======
Purpose
=======

UMM is used to simplify the batch processing of video and image sequences that are output by Q3MME.
It allows you to mux all of the output into .avi files with audio (when available).

============
Installation
============

UMM requires .NET Framework 4.0 Client Profile at a minimum to to run.
You can get it here: http://www.microsoft.com/en-us/download/details.aspx?id=24872
If the link doesn't work, seach for "Microsoft .NET Framework 4 Client Profile (Standalone Installer)".

UMM requires MEncoder, which is part of the free and open source MPlayer video player package.
It doesn't need any installation, you can extract it anywhere (even in Program Files).
Only the version described as "for 32-bit Windows" will work with VFW codecs, so you should grab that one.
You can find it here: http://mplayerwin.sourceforge.net/downloads.html

If you want to use a VFW codec (like Lagarith, for instance), you have to copy the .dll file(s) to "$(MPlayer_folder)\codecs".

UMM itself creates a Config.xml right next to the executable so it's best to:
1. Have UMM in its own folder, so the Config.xml stays with the executable.
2. Not have UMM in Program Files or Program Files (x86)

==========
Setting up
==========

Open up UMM.exe and navigate to the "Settings" tab.
Under "General Settings", make sure you fill in the "MEncoder Path" text box (the file path of mencoder.exe).

=========
Use Cases
=========

Option: output all files to this folder?            | No
---------------------------------------------------------------------------------------------------------------------------------------------------
Input: .avi                                         | Output: new .avi file in the same folder, named according to the "File Naming Rules" settings
Input: folder with .avi file(s) and optional .wav   | Output: new .avi file in the parent folder, called $(input_folder_name).avi
Input: folder with image file(s) and optional .wav  | Output: new .avi file in the parent folder, called $(input_folder_name).avi
- if it has a depth sequence (*.depth.*.$(ext))     | - $(input_folder_name)_depth.avi, uses the "Monochrome Video CODEC" settings, has no sound
- if it has a stencil sequence (*.stencil.*.$(ext)) | - $(input_folder_name)_stencil.avi, uses the "Monochrome Video CODEC" settings, has no sound

Option: output all files to this folder?            | Yes
-----------------------------------------------------------------------------------------------------------------------------------------------------
Input: .avi                                         | Output: new .avi file in the custom folder, named according to the "File Naming Rules" settings
Input: folder with .avi file(s) and optional .wav   | Output: new .avi file in the custom folder, called $(input_folder_name).avi
Input: folder with image file(s) and optional .wav  | Output: new .avi file in the custom folder, called $(input_folder_name).avi
- if it has a depth sequence (*.depth.*.$(ext))     | - $(input_folder_name)_depth.avi, using the "Monochrome Video CODEC" settings, has no sound
- if it has a stencil sequence (*.stencil.*.$(ext)) | - $(input_folder_name)_stencil.avi, using the "Monochrome Video CODEC" settings, has no sound

==================
Settings Explained
==================

"Video CODEC" settings:
- Raw:      this will actually transform the input into a color-space/codec of MEncoder's choosing
- Copy:     this will tell MEncoder to leave the data untouched whenever possible
            note that .tga inputs will create MTGA (Motion TGA) outputs and .png inputs will create MPNG (Motion PNG) outputs
- Lagarith: use the Lagarith lossless codec. this codec only supports rgb(a) and yuv color-spaces
- Lavc:     use a codec supported by the libavcodec library

"Monochrome Video CODEC" settings are only used for depth/stencil image sequences:
- depth sequences are those including the string ".depth." in the file names
- stencil sequences are those including the string ".stencil." in the file names

==============
Special Thanks
==============

id Software
The Q3MME developers
The MPlayer/MEncoder developers
farnish: feedback on the installation/set-up process