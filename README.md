# Filesystem-Toolbox

[![License](https://img.shields.io/github/license/Hawkynt/Filesystem-Toolbox)](https://github.com/Hawkynt/Filesystem-Toolbox/blob/master/LICENSE)
[![Language](https://img.shields.io/github/languages/top/Hawkynt/Filesystem-Toolbox?color=8957D5)](https://github.com/Hawkynt/Filesystem-Toolbox)

[![CI](https://github.com/Hawkynt/Filesystem-Toolbox/actions/workflows/ci.yml/badge.svg?branch=master)](https://github.com/Hawkynt/Filesystem-Toolbox/actions/workflows/ci.yml)
![Last Commit](https://img.shields.io/github/last-commit/Hawkynt/Filesystem-Toolbox?branch=master)
![Activity](https://img.shields.io/github/commit-activity/m/Hawkynt/Filesystem-Toolbox)

[![Stars](https://img.shields.io/github/stars/Hawkynt/Filesystem-Toolbox?color=FFD700)](https://github.com/Hawkynt/Filesystem-Toolbox/stargazers)
[![Forks](https://img.shields.io/github/forks/Hawkynt/Filesystem-Toolbox?color=008080)](https://github.com/Hawkynt/Filesystem-Toolbox/network/members)
[![Issues](https://img.shields.io/github/issues/Hawkynt/Filesystem-Toolbox)](https://github.com/Hawkynt/Filesystem-Toolbox/issues)
![Code Size](https://img.shields.io/github/languages/code-size/Hawkynt/Filesystem-Toolbox?color=4CAF50)
![Repo Size](https://img.shields.io/github/repo-size/Hawkynt/Filesystem-Toolbox?color=FF9800)

[![Release](https://img.shields.io/github/v/release/Hawkynt/Filesystem-Toolbox?sort=semver)](https://github.com/Hawkynt/Filesystem-Toolbox/releases/latest)
[![Nightly](https://img.shields.io/github/v/release/Hawkynt/Filesystem-Toolbox?include_prereleases=true&sort=date&label=nightly&color=FF9800)](https://github.com/Hawkynt/Filesystem-Toolbox/releases)
[![Downloads](https://img.shields.io/github/downloads/Hawkynt/Filesystem-Toolbox/total)](https://github.com/Hawkynt/Filesystem-Toolbox/releases)

Tools to mess with the filesystem

TODO: 
  always visible in systray - single instance application
  function: check file consistency
    * let user configure folders to be observed
    * create database file containing hashes of files in directory and all subdirectories
      * class implementing IDictionary<string,string>
      * single file for all hashes
      * starting at eof is size of kvp to skip backwards to next kvp
      * add key stored at end of file
      * update key stored at end of file, mark previous key as deleted
      * deleted keys stored as marker
      * periodically optimize file, removing all updated keys
      * in-memory cache class implementing IDictionary<string,string>
    * install FSW to update checksums whenever files get modified/created/deleted
    * periodically check folders against db and inform user with list of (broken) files
      * ask for confirmation and update db
    * allow configuring task to execute when broken files occur, allow task on each individual file and for all files at once
  function: find-duplicates and replace with hard-links on ntfs
    * allow setting read-only attribute for all hard-links to avoid ntfs hard-link behavior bug (ie. does not copy on write)
