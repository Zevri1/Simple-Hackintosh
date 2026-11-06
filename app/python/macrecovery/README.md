# macrecovery source and license

This directory contains the macrecovery utility copied from:

- Upstream repository: https://github.com/acidanthera/OpenCorePkg
- Upstream directory: `Utilities/macrecovery/`
- Upstream tree snapshot: `8d8d690cb543f63114340cdae958de4ce7a8296f`

The upstream Python source and Windows launcher are retained with their original copyright notices. The upstream BSD 3-Clause license text is included in `LICENSE.txt`. The `boards.json` file is included because the upstream utility uses it for its optional board/MLB lookup action.

The Recovery image itself is not part of this source copy. macrecovery requests and downloads Recovery files from Apple's Recovery servers when the user runs its `download` action.
