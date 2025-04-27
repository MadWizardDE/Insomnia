# Insomnia

System service for fine-grained monitoring of resource usage and sleep control. Features event-based action triggers and provides an extensible framework for customization. 

## Why should I need this?

Insomnia is for those who find, that the Windows' built-in method for power requests is too inflexible and error prone. The main target group are people, who operate a Windows system as a home lab or some other kind of headless server. It is implemented as a background service, that takes control over sleep management, monitoring a hand-picked list of OS resources and initiates preconfigured actions accordingly.

## Features

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

### FileShareMonitor (🚧 under construction 🚧)

- Select individual file shares that, when accessed, will stop the system from going to sleep
    - filter by remote username
    - filter by file path


