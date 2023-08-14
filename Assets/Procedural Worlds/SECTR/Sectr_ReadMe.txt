Welcome and thanks for buying Sectr!

! Please don't delete or move this file as it is used to determine the Sectr root directory. !

** Version history **

Version 2019.0.3

- Reviewed for Unity 2019.3 support, removed obsolete warnings.
- Fixed assets throwing serialization errors in 2019.3
- Fixed "SRP fix" checkbox for Sectr Vis in 2019.3
- Fixed various smaller issues with the Sectr Audio Window
- Fixed potential issues with cross / circular references in the Audio template system
- Added a "Remove template" button to revert template association
- Added a new option to recycle scenes when exporting in Streaming Window: Recycling scenes is a bit slower when exporting, but will in return not create new scene entries in the build settings which can lead to problems in some projects. Recommended to leave off until needed.
- Added a new option to save scenes automatically when exporting single chunks in Streaming Window
- Added an "update mode" for Sectr Member / Sector scripts: You can now choose how often to do child / bounds calculations for Sector / Member scripts. This can be used to combat editor lag in large scenes with a lot of sectors and objects.
- Fixed Export button not selecting sector sometimes in Streaming Window
- Fixed an issue where an AudioSystem component could be deleted from prefabs when the prefab was viewed in prefab edit mode
- Reviewed the Check for older Sectr installations, this check will now only performed once after installation, if no old install was found the check will be disabled
- Added a get accessor for AudioSystem Instances so it is easier for user code to evaluate the running audio instances
- Fixed an issue where the scene hierarchy context menu would create multiple warning messages when creating a new sector
- Fixed a bug with Sectr VIS where a wide FOV / Aspect Ratio could lead to sectors not being rendered anymore

Version 2019.0.2

- Improved export so that sector child objects will be exported in the same order as they were in the original sector
  Thanks to discord user @ropemonkey for the suggestion!
- Improved speed for reverting sectors with a deeper child structure
  Thanks to discord user @Pilgrim for the suggestion!

Version 2019.0.1

- Fixed an issue with Stream Export where exported objects in Sectors would lose their prefab state
- Added a message box when GameObjects could not be sorted away by the Drag and Drop Box to inform the user
- Changed the options to spawn GameObjects spawned by Gaia so that it will only process activated spawners
- When deactivating the terrain split option in the terrain window, the window will now remember your settings when switching through different terrains.
- Reviewed the documentations, added proper PDF bookmarks for better navigation
- Fixed an issue that could lead to build errors in Unity 2017 and below.
- Fixed 2 obsoletion warnings
- Stabilized the package import process under Unity 2019.1 and above

Version 2019.0.0, Initial version. 