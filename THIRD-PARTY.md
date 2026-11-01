# Third-party components

This repository does not contain any third-party binaries. Everything listed below is downloaded directly from NuGet by `build.ps1` on the machine that builds the project.

| Library | Version | Licence | Used for |
| --- | --- | --- | --- |
| [LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) | 0.9.6 | MPL-2.0 | Reading CPU temperature through the PawnIO driver |
| [HidSharp](https://github.com/IntergatedCircuits/HidSharp) | 2.6.4 | Apache-2.0 | Internal dependency of LibreHardwareMonitorLib |
| [DiskInfoToolkit](https://github.com/LibreHardwareMonitor/DiskInfoToolkit) | 1.1.2 | MIT | Internal dependency of LibreHardwareMonitorLib |
| [RAMSPDToolkit-NDD](https://github.com/LibreHardwareMonitor/RAMSPDToolkit-NDD) | 1.4.2 | MIT | Internal dependency of LibreHardwareMonitorLib |
| System.Memory, System.Buffers, System.Numerics.Vectors, System.Runtime.CompilerServices.Unsafe, System.Threading.AccessControl | see `build.ps1` | MIT | Base .NET libraries |

## PawnIO driver

CPU temperature can only be read with the help of a kernel driver. This project uses [PawnIO](https://pawnio.eu/) by namazso, which is digitally signed and compatible with Windows Memory Integrity.

PawnIO is **not** part of this repository and is **not** installed automatically. The user installs it from its official release. PawnIO is governed by its own project licence.

## Note on MPL-2.0

`LibreHardwareMonitorLib` is licensed under MPL-2.0. Because this repository only contains its own source code and downloads that library at build time, no MPL-2.0 binaries are redistributed here. If you redistribute a modified version of that library together with this project, you must make its source available as required by MPL-2.0.

## Trademarks

NVIDIA, Intel, MSI and other product names are trademarks of their respective owners. This project is not affiliated with any of them.
