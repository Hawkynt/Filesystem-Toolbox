# Filesystem-Toolbox
Tools to mess with the filesystem

TODO: 
  always visible in systray - single instance application
  function: check file consistency
    * let user configure folders to be observed
    * create database file containing hashes of files in directory and all subdirectories
      * class implementing IDictionary<string,string>
      * single file for all hashes
      * starting at eof is size of kvp to skip backwards to next kvp
      * add/update key stored at end of file
      * deleting keys stored as marker in first and last key appearance
      * periodically optimize file, removing all updated keys
      * in-memory cache class implementing IDictionary<string,string>
    * install FSW to update checksums whenever files get modified/created/deleted
    * periodically check folders against db and inform user with list of (broken) files
      * ask for confirmation and update db
    * allow configuring task to execute when broken files occur, allow task on each individual file and for all files at once
  function: find-duplicates and replace with hard-links on ntfs
    * allow setting read-only attribute for all hard-links to avoid ntfs hard-link behavior bug (ie. does not copy on write)
