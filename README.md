#OpenStore Report Plugin

##Introduction

The OpenStore report plugin will give SQL report functionality to OpenStore.

The plugin can be found in the Admin Panel "Admin>Reports"

##Security

OS_Report is deisgned to strip out any update commands, so only SQL view reports can be run.

**WARING: SQL in OS_Report always work across ALL the database.  Therefore, report creation and update is limited to HOST only**


##Installation

The installation package is installed like a normal DNN module.

It will be installed under "/DesktopModules/NBright/OS_Reports"

##SQL

Any SQL to select data is possible in OS_Report, but to create SQL reports for OpenStore you can use the views created for OpenStore.

https://github.com/openstore-ecommerce/Developer-Documents/blob/master/SQL-Architecture.pdf

