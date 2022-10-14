# Service Broker Listener with  SqlDependecyEx
Cross-platform .NET 4.0 (C# 4.0) and .NET Core compatible component which receives SQL Server table changes into your .net code.

Take a look how it works: http://sbl.azurewebsites.net

# How To Use

1. Copy [SqlDependecyEx](https://github.com/dyatchenko/ServiceBrokerListener/blob/master/ServiceBrokerListener/ServiceBrokerListener.Domain/SqlDependencyEx.cs) class from `ServiceBrokerListener.Domain` project into your solution.
2. Make sure that Service Broker is enabled for your database.
    
    ```
    ALTER DATABASE test SET ENABLE_BROKER
    
    -- For SQL Express
    ALTER AUTHORIZATION ON DATABASE::test TO userTest
    ```
    
3. Use the class as in example below:

    ```
    // See constructor optional parameters to configure it according to your needs
    var listener = new SqlDependencyEx(connectionString, "YourDatabase", "YourTable");
    
    // e.Data contains actual changed data in the XML format
    listener.TableChanged += (o, e) => Console.WriteLine("Your table was changed!");
    
    // After you call the Start method you will receive table notifications with 
    // the actual changed data in the XML format
    listener.Start();
    
    // ... Your code is here 
    
    // Don't forget to stop the listener somewhere!
    listener.Stop();
    ```

4. Enjoy!

# How to use for multiple tables

All you need to do is to create multiple listeners with different identities as shown below:

    var listener1 = new SqlDependencyEx(connectionString, "YourDatabase", "YourTable1", identity: 1);
    var listener2 = new SqlDependencyEx(connectionString, "YourDatabase", "YourTable2", identity: 2);
    
# How to use in multiple apps

You **must** create listeners with **unique** identities for each app. So, only one listener with a specific identity should exist at the moment. This is made in order to make sure that resources are cleaned up. For more information and best practices see @cdfell [comment](https://github.com/dyatchenko/ServiceBrokerListener/issues/29#issuecomment-241826532) and [this](https://github.com/dyatchenko/ServiceBrokerListener/issues/29#issuecomment-241943608) answer.

Application 1:

    // identities are different
    var listener = new SqlDependencyEx(connectionString, "YourDatabase", "YourTable", identity: 1);
    
Application 2:

    // identities are different
    var listener = new SqlDependencyEx(connectionString, "YourDatabase", "YourTable", identity: 2);
    
# How to track UPDATEs only

The `listenerType` constructor parameter configures `SqlDependencyEx` to fire an event for
different notification types (can fire on INSERT, UPDATE, DELETE separately or together)

    var listener = new SqlDependencyEx(connectionString, "YourDatabase",
                 "YourTable1", listenerType: SqlDependencyEx.NotificationTypes.Update);
    
# Licence

[MIT](LICENSE)
