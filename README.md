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
Utilizes [npcap](https://npcap.com/) to monitor incoming network packets.

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

Incarnates a little Helper Process in each session, to allow manual control of the sleep cycle.

- set a indefinite sleepless mode
- set a time based sleepless mode
- disable the usage based sleepless mode temporarily

### DuoStreamMonitor

For those who are enthusiastic users of [DuoStream](https://github.com/DuoStream), this plugin makes Insomnia aware of the configured instances.

- start instances on demand, when they are accessed by a Moonlight client (no clientside configuration needed)
- stop instances after they become idle, to reduce power consumption of the GPU and to reduce the overall footprint of system resources
