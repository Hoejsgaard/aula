A small implementation to integrate to Unilogin to get an access token to Aula and MinUddannelse.

This is for sure not as intended, but I was unable to find a way to setup authentication with tokens or certificates. 

Works fine locally, but once deployed to Azure (only provider I gave a shot) it wont work , as they will introduce CAPTCHA. This can probably be circumvented with a proxy, since the image runs fine on my machine, but I could not be bothered. Instead I'm extracting the data with a cronjob on my local server (running at 16:00 CET, since there seem to be maintenance of minuddannelse or the IDP every Sunday at 10:00).

Leaving this public in case there are others that would like week schedules and the like, pushed to other channels. 

Was planning on making slack commands for Aula messages and announcemnet, but settled for just having the schedule copied to google calendars and the week notes echoed to Slack & Telegram.
