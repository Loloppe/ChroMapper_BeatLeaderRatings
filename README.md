# Ratings

## ChroMapper plugin

Display BeatLeader Pass, Tech, Acc and Star rating for the full map and for an average of notes while mapping  

### How to install
Download the latest release (Ratings.zip) here: https://github.com/Loloppe/ChroMapper_Ratings/releases/latest  
Extract the .zip inside your ChroMapper folder  
Your Plugins folder should now include the files: Ratings.dll, model_sleep_bl.onnx
The ChroMapper_Data\Plugins\x86_64 folder should include: onnxruntime.dll

### How to use

This plugin with load automatically on map load and run automatically  
You can press Tab while mapping, a Ratings text icon should appear on the menu on the right side of the screen  
Clicking that icon will open the Ratings menu  
If you want to reload the map, first save the map, then press the Reload Map button  

### Options

- Enabled : Can be used to hide the UI and text  
- Timescale : Simulate a different speed for the map (SS: 0.85, FS: 1.2, SFS: 1.5). You must Reload Map for this to apply  
- Note Count : Change how many notes to uses from cursor and onward for the average
- Star Calc : Change the % accuracy used to calculate the star rating (default 0.96)
