# Insomnia

System service for fine-grained monitoring of resource usage and sleep control. Features event-based action triggers and provides an extensible framework for customization. 

## Why should I need this?

Insomnia is for those who find, that the Windows' built-in method for power requests is too inflexible and error prone. The main target group are people, who operate a Windows system as a home lab or some other kind of headless server. It is implemented as a background service, that takes control over sleep management, monitoring a hand-picked list of OS resources and initiates preconfigured actions accordingly.

## Core Features

Features marked with a construction sign (🚧) are not fully operational yet, but will be released very soon.

### SessionMonitor

- Select user that should keep the system running, while using the computer
    - Takes the standard LastInputTime of the Windows session into account
    - Designate processes with an activity threshold, that will also count as user activity
- Configure actions to take, when the session becomes idle (lock, logout, disconnect, execute a program/script, ...)

### NetworkMonitor
Utilizes the free [npcap](https://npcap.com/) library to monitor incoming network packets.

- Define arbitrary network services (by port usage) that will stop the PC from suspending, while traffic is registered
- Configure triggers to take actions when a service is accessed or starts to idle

- Support for Hyper-V included
    - Can start virtual machines, when they are accessed by a network service
    - Can stop/suspend virtual machines, when they are not accessed anymore

### ProcessMonitor

- Designate processes with an activity threshold, that will also count as system activity

### PowerRequestMonitor

- Create whiteliste filters for the built-in power requests, to should be allowed to keep the system awake.

### 🚧 FileShareMonitor

- Select individual file shares that, when accessed, will stop the system from going to sleep
    - filter by remote username
    - filter by file path

## Additional Features

To excercise the open architecture of the framework, some of the more specific features were developed and packaged as a plugin, that can be added and removed any time.

### 🚧 Interactive Taskbar Icon

Incarnates a little Helper Process in each session, that communicates with the service to display information and to allow manual control of the sleep cycle.

- set a indefinite sleepless mode
- set a time based sleepless mode
- disable the usage based sleepless mode temporarily

### DuoStreamMonitor

For those who are enthusiastic users of [DuoStream](https://github.com/DuoStream), this plugin makes Insomnia aware of the configured instances.

- start instances on demand, when they are accessed by a Moonlight client (no clientside configuration needed)
- stop instances after they become idle, to reduce power consumption of the GPU and to reduce the overall footprint of system resources

### etc.

If you find, that a cruscial feature is missing yet, don't hesitate to open an issue and explain why Insomnia should have support for your use case. Alternatively if you are adept at programming C#, you can check out the provided 🚧 **example project** and develop your own extension plugin, to make Insomnia aware of you special resource.

## System Requirements

- Windows 8 / 10 / 11
- .NET 8 / .NET Framework 4.8
- pcap (only needed for the NetworkMonitor)

### How to get started

A considerable amount of development time was invested to provide you with a sophisticated installer, that allows you to set everything up and running in a minute.

It does the work for you, to register Insomnia as a system service, download and install all necessary dependencies, guide you through a basic configuration of the parameters. Nevertheless, you are encouraged to dive into the 🚧 **Wiki** to discover, what Insomnia can do for you and how to configure it.

If it happens that you decide against using Insomnia, the installer will help you to remove everything from your system completely. For your convenience, you can run the installer again (or hit "Modify" in the system settings) to add/remove some of the optional features later on.

🪄 Just download the latest release from GitHub and follow the steps of the wizard.
