# Database.cs
A very simple database helper file

## Requirements
- .Net Framework 3.5 or higher version (with LInQ)
    - or .Net Core 1.0 or higher version

## Quick Start
1. Copy Database.cs to anywhere of your project.
2. This class (`DLMSoft.DBCS.Database` by default) contains no database connection, so you should create a database connection for yourself. Such as :
```
using System.Configuration;
using System.Data.SqlClient;
class DBConnections {
    public SqlConnection Default => new SqlConnection(
        ConfigurationManager.ConnectionStrings["default"].ConnectionString);
}
```
3. When you want to access database, create a `Database` instance by `using` block :
```
using (var db = new Database(DBConnections.Default)) {
    // Do anything you want with 'db'.
}
```
4. Enjoy yourself !

## Examples
### Query from database
No parameters, multi-rows :
```
using (var db = new Database(conn)) {
    var user_list = db.Query<dynamic>("SELECT * FROM Users");
    foreach (var user in user_list) {
        Console.WriteLine(user.Username);
    }
}
```

Parameterized, single row :
```
using (var db = new Database(conn)) {
    var user = db.Query<dynamic>("SELECT * FROM Users WHERE Username = @username", new {
        username = "' or 1 = 1 --"
        // TODO add more parameters here.
        }).FirstOrDefault();
    if (user == null) {
        // TODO this means there have no username "' or 1 = 1 --" in database.
    }
    else {
        // TODO do anything with 'user'.
    }
}
```

### Transaction insertion
```
var model = YOUR_MODEL_WITH_CHILDREN_ITEMS_HERE;
using (var db = new Database(conn)) {
    int affected_rows;
    db.BeginTransaction();
    try {
        affected_rows = db.Execute("INSERT INTO Merchant_Categories(Name) VALUES(@Name)", model);
        if (affected_rows == 0)
            throw new Exception("Failed to create data !");
        var category_id = db.Scalar<int>("SELECT @@IDENTITY");
        foreach (var merchant_info in model.Merchants) {
            merchant_info.CategoryId = category_id;
            affected_rows = db.Execute("INSERT INTO Merchant_Informations(Name, Price, Quantity, CategoryId) VALUES(@Name, @Price, @Quantity, @CategoryId)", merchant_info);
            if (affected_rows == 0)
                throw new Exception("Failed to create data !");
        }

        db.CommitTransaction();
    }
    catch (Exception ex) {
        db.RollbackTransaction();
    }
}
```