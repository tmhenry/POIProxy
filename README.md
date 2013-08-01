PROXY Application for POI

Deployment instruction:

Step 1: publish the proxy web application to a local folder

1. Open the proxy visual studio sln
2. Right click POIProxy in the solution explorer and choose Publish
3. Click on profile tab to choose an existing publish profile or 
   create a new profile by clicking the drop-down menu to the left of Import button
4. If choosing existing profile, configure the connection tab and use target server IP as destination URL
   If creating a new one, choose FILE System as the deployment method, and configure the destination URL
5. The target location should be a folder where you want to store all the site related file in local file system
6. Click publish to finish

Step 2: open the IIS service on the server

1. Open control pannel, choose programs and choose turn on or off windows features
2. Tick the Internet Information Service
3. Expand Internet Information Service and then Web Management Tools, tick IIS Management service
4. Expand World Wide Web Service and then Application Development Features, tick all except CGI

Server 2008 version:
1. Open server manager, choose Roles and add role
2. Select web server and select all asp.net development features

Step 3: Download IIS Manger (7.0) from the web

Step 4: Register .net 4.5 for IIS

1. Open cmd in admin mode
2. CD to directory C:\Windows\Microsoft.NET\framework(64)\v4.0...(some version number)\
3. Run aspnet_regiis -i in the console until you see successful message

PS: if you see managementPipeLine module missing, it's because incorrect setup in this step

Step 5: Copy the published site into IIS wwwroot

1. Open C:\inetpub\wwwroot
2. Copy the published site into this folder

PS: You may get access permission denied error if the site is not copied into this directory
PS: Don't forget to add c://windows/temp all control auth.

Step 6: Configure site setting on IIS

1. Open IIS manager, remove the default sites under sites tab
2. Right click the sites tab and choose add new site
3. Name the new site POIProxy (or any other name, not tested though)
4. Choose the folder inside wwwroot where you put the new published site
5. Select the server IP as the IP Address in the binding options
6. click Application Pools, select POIProxy and change .net framework version to v4.0..

After all steps done, you should see "Server Error in / Application" message when you open the server IP in browser
Furthermore, you should see a js when you open IP/signalr/hubs in browser
