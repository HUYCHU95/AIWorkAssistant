# DevExtreme local licensed setup
The standard dashboard works offline with SQLite. DevExtreme is OPTIONAL and NOT included: its commercial license/assets were not supplied, so its runtime acceptance gate is BLOCKED, not a PASS.

With a valid DevExpress license, place matching-version assets in `Data/LicensedUI/`: `dx.all.js`, `dx.light.css`, `dx.dark.css`, `devextreme-license.js`. The last file must configure your legitimately issued key following the vendor's instructions, after dx.all.js is loaded. Do not commit it. Set `"DevExtreme":{"Enabled":true}` in appsettings.json and restart.

The adapter instantiates dxChart, dxPieChart and dxDataGrid over the same `/Dashboard/Data` SQLite API; grid writes use `/Dashboard/UpdateTask` with CSRF/version validation. No trial, generated key or CDN is bundled. Compatibility with the locally supplied DevExtreme release and its CSP requirements must be verified after setup. Native timeline shows only actual stored dates and dependency links.

Vendor licensing: https://js.devexpress.com/Licensing/ and https://js.devexpress.com/jQuery/Documentation/Guide/Common/Licensing/.
