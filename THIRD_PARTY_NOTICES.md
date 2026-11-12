# Third-Party Notices

This repository includes or interacts with the following third-party software:

## ViGEmClient (`vendor/vigemclient.dll`)
- **Project:** ViGEmClient (https://github.com/nefarius/ViGEmClient)
- **Author:** Nefarius Software Solutions e.U. and contributors
- **License:** MIT License
- **Notes:** `vigemclient.dll` is the native x64 C/C++ client library used by Twin2XInput via P/Invoke to communicate with the ViGEmBus kernel driver.

## ViGEmBus Driver (Required Runtime Dependency — Not Bundled)
- **Project:** ViGEmBus (https://github.com/nefarius/ViGEmBus)
- **Author:** Nefarius Software Solutions e.U. and contributors
- **License:** BSD-3-Clause
- **Download:** https://github.com/nefarius/ViGEmBus/releases
- **Notes:** End users must install `ViGEmBusSetup_x64.msi` once on Windows so virtual Xbox 360 controllers can be created.

