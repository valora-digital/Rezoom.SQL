namespace Rezoom.SQL.Mapping

open System
open System.Configuration
open System.Data.Common
open System.Reflection

[<AbstractClass>]
type ConnectionProvider() =
    abstract member Open : name : string -> DbConnection
    abstract member BeginTransaction : DbConnection -> DbTransaction
    default __.BeginTransaction(conn) = conn.BeginTransaction()

module NetStandardHacks =
    let loadInstance (assemblyName : string) (typeName : string) =
        let asm = Assembly.Load(assemblyName)
        if isNull asm then failwithf "Couldn't load assembly %s" assemblyName
        let ty = asm.GetType(typeName)
        if isNull ty then failwithf "Couldn't load type %s from assembly %s" typeName assemblyName
        let instance = ty.GetProperty("Instance", BindingFlags.Public|||BindingFlags.NonPublic|||BindingFlags.Static)
        if isNull instance then
            let instance = ty.GetField("Instance", BindingFlags.Public|||BindingFlags.NonPublic|||BindingFlags.Static)
            if isNull instance then
                Activator.CreateInstance(ty) :?> DbProviderFactory
            else
                instance.GetValue(null) :?> DbProviderFactory
        else
            instance.GetValue(null) :?> DbProviderFactory
    type DbProviderFactories() =
        static member GetFactory(providerName : string) : DbProviderFactory =
            match providerName.ToLowerInvariant() with
            | "Microsoft.Data.SqlClient" ->
                loadInstance "System.Data" "Microsoft.Data.SqlClient.SqlClientFactory"
            | "system.data.sqlite" ->
                loadInstance "System.Data.SQLite" "System.Data.SQLite.SQLiteFactory"
            | "npgsql" ->
                loadInstance "Npgsql" "Npgsql.NpgsqlFactory"
            | "microsoft.data.sqlite" ->
                loadInstance "Microsoft.Data.Sqlite" "Microsoft.Data.Sqlite.SqliteFactory"
            | other ->
                failwithf "Tragically unsupported provider name ``%s``" providerName
open NetStandardHacks

type DefaultConnectionProvider() =
    inherit ConnectionProvider()

    static let mutable configurationReader : string -> ConnectionStringConfig = (fun _ -> failwith "no implementation was provided")

    static member SetConfigurationReader (reader: string -> ConnectionStringConfig) =
        configurationReader <- reader

    static member ResolveConnectionString(name : string) =
        configurationReader name

    static member Open(name) =
        let connectionString = DefaultConnectionProvider.ResolveConnectionString(name)
        let provider : DbProviderFactory = DbProviderFactories.GetFactory(connectionString.ProviderName)
        let conn = provider.CreateConnection()
        conn.ConnectionString <- connectionString.ConnectionString
        conn.Open()
        if conn.GetType().Name = "SQLiteConnection" then
            // Encourage SQLite to put the R in RDBMS
            use cmd = conn.CreateCommand()
            cmd.CommandText <- "PRAGMA foreign_keys=ON;"
            cmd.ExecuteNonQuery() |> ignore
        conn

    override __.Open(name) = DefaultConnectionProvider.Open(name)


