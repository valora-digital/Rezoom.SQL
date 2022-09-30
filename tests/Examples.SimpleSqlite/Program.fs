open Rezoom
open Rezoom.SQL
open Rezoom.SQL.Mapping
open Rezoom.SQL.Migrations
open Rezoom.SQL.Synchronous

type MyModel = SQLModel<".">

let migrate() =
    // customize the default migration config so that it outputs a message after running a migration
    let config =
        { MigrationConfig.Default with
            LogMigrationRan = fun m -> printfn "Ran migration: %s" m.MigrationName
        }
    // run the migrations, creating the database if it doesn't exist
    MyModel.Migrate(config)


// define a SQL command for inserting a user and getting their ID
type InsertUser = SQL<"""
    insert into Users(Name, Email) values (@name, @email);
    select last_insert_rowid() as id
""">

// define a SQL command for inserting a comment
type InsertComment = SQL<"insert into Comments(AuthorId, Comment) values (@authorId, @comment)">

let addExampleUser name email =
    // open a context in which to run queries
    use context = new ConnectionContext()
    // insert a user and get their ID
    let userId : int64 =
        InsertUser.Command(email = email, name = Some name).ExecuteScalar(context)
    // add a couple comments by the user
    InsertComment.Command(authorId = int userId, comment = "Comment 1").Execute(context)
    InsertComment.Command(authorId = int userId, comment = "Comment 2").Execute(context)

type ListUsers = SQL<"""
    select user.Id, user.Name, user.Email from Users as user
    order by user.Name
""">

let showUsers() =
    use context = new ConnectionContext()
    let users = ListUsers.Command().Execute(context)
    printfn "There are %d users." users.Count
    for user in users do
        printfn "User ID %d's email is %s..." user.Id user.Email
        match user.Name with
        | None -> printfn "  and they don't have a name."
        | Some name -> printfn "  and their name is %s." name

[<EntryPoint>]
let main argv =
    DefaultConnectionProvider.SetConfigurationReader (fun connectionName ->
        match connectionName.Equals "rzsql" with
        | true -> { ConnectionString = "Data Source=rzsql.db"; ProviderName = "Microsoft.Data.SQLite" }
        | false -> failwithf "unknown connection name '%s'" connectionName
    )

    migrate()
    addExampleUser "Peter" "Parker"
    showUsers()
    // return 0 status code
    0