# Insomnia

System service for fine-grained monitoring of resource usage and sleep control. Features event-based action triggers and provides an extensible framework for customization. 

## Why should I need this?

Insomnia is for those who find, that the Windows' built-in method for power requests is too inflexible and error prone. The main target group are people, who operate a Windows system as a home lab or some other kind of headless server. It is implemented as a background service, that takes control over sleep management, monitoring a list of hand-picked list OS resources and initiates preconfigured actions accordingly.
