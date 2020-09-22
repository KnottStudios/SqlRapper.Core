# SqlRapper
A lightweight ORM alternative that supports bulk inserts and Sprocs

Check out the Tests folder to see how to use this project.  


READ:
1. Create a class that represents the model you want HOWEVER:
2. If the model's names match the table column names, those columns will be populated, otherwise they won't. 
3. However, you can write custom sql in the GetData and alias your names.
4. If you class name matches the table name minus the 's' then the service will write your table name appropriately, otherwise pass it in.  Log becomes the Logs table.

READ Example using a Log object:
db.GetData<Log>("WHERE ApplicationId = 2", "Logs");

or if we wanted all the items:
db.GetData<Log>();


INSERT:
1. As above, create a class that represents the table.  
2. Properties that match column names will be populated; if there are properties that aren't on the table, that will throw an exception.  
3. To ignore a primary key put a [primarykey] attribute over the column.  In that instance, the column will not be inserted.
4. To ignore a default key and have the database generate that key, put a [defaultkey] attribute over that column.  That column will also not be inserted IF IT IS NULL.

INSERT Example using a Log object:
db.InsertData(log);

or if we had a list of logs:
db.BulkInsertData(logs);


UPDATE:
1. Similar to an insert.  Ignores the primarykey attribute when there is a where clause.  If there is not a where clause, it uses the primary key for the where clause.
2. Does not set any Null field.
3. Only sets fields with values.

UPDATE example using a Log object with a primary key:
db.UpdateData(log);

or with a where clause:
db.UpdateData(log, "Where LogId = 32")
