# TrayHeartRate

Queries heart rate from Oura Ring API and displays it in the Windows system tray.

It is NOT a real time monitor, you may need to run Oura App on the phone to get the heart rate synchronized.
So far I could not find a way to get real time heart rate from Oura Ring as I hoped to. So strictly speaking this project is a failure.
If you find a way to force Oura Ring to sync heart rate more often, please let me know.

You need to download https://github.com/ivan-shaporov/OuraRing repo and put it in the parent folder next to this repo.

Either use Visual Studio "Manage User secrets" feature or open. `%AppData%\Microsoft\UserSecrets\f8ecf344-ca76-41c4-b9f8-d2b7b8349c1f\secrets.json` and add the following:

```json
{
  "oura_personal_token": "your Oura personal token",
}
```
