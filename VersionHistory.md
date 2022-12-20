# Version History

## 3.4.1

* Handle SQLite interruption in Get statements.

## 3.4.0

* Add `SQLiteConnection.BeginTransaction(bool deferred)`: [#53](https://github.com/Faithlife/System.Data.SQLite/pull/53).

## 3.3.4

* Improve efficiency of calling `IsDBNull` then `GetValue` (for the same field).

## 3.3.3

* Fix failure to bind an empty `byte[]` as a SQLiteParameter value.
  * This regression was introduced in 3.3.1.

## 3.3.2

* Fix `InvalidCastException` reading the results of an expression or subquery.
  * This regression was introduced in 3.3.0.

## 3.3.1

* Support `ArraySegment<byte>`, `Memory<byte>` and `ReadOnlyMemory<byte>` as parameter values.

## 3.3.0

* Add `SQLiteDataReader.GetReadOnlySpan` (`net5.0` only) to return direct access to SQLite's memory for `BLOB` and `TEXT` columns.
* Implement`SQLiteDataReader.GetFieldValue<T>`.
* Implement `GetInt32`, `GetInt64`, `GetGuid`, `GetDouble`, etc., without boxing a temporary value.
* Implemented synchronous `Read` and `NextResult` methods slightly more efficiently.

## 3.2.5

* Eliminate allocations in common code paths.

## 3.2.4

* Fix bug introduced in 3.2.3 that failed to bind a parameter value set to the empty string (`""`).

## 3.2.3

* Workaround limitation with AMD64 varargs methods on Unix (macOS) when initializing logging in `SQLiteLog`.
* Performance optimizatons:
  * Make `StatementCompletedEventArgs.Sql` lazy.
  * Remove use of `Regex` for parsing `DataSource`. 
* Performance optimizations for .NET 5.0:
  * Use `stackalloc` for temporary allocations.
  * Use `ArrayPool<byte>` for longer-lived allocations.

## 3.2.2

* Move DllImportResolver code to `ModuleInitializer` to workaround differences in Mono runtime.
  * Mono does not run the static constructor before evaluating the `DllImport` library if the first use of the class is to a P/Invoke method.

## 3.2.1

* Add `net5.0` target platform.

## 3.2.0

* `SQLiteLog` only calls `sqlite3_config` to initialize logging when an event handler is attached to `SQLiteLog.Log`.
  * It is no longer called automatically from the `SQLiteConnection` constructor. This fixes a `TypeInitializationException` and `AppDomainUnloadedException` in `SQLiteLog`.
  * If you are using `SQLiteLog.Log`, you _must_ attach an event handler before calling any other methods of this library. This is a [limitation of SQLite](https://www.sqlite.org/c3ref/config.html).
  * If other methods in this library are used before `SQLiteLog.Log`, the native event handler will fail to be installed properly, and no `Log` events will be raised. 

## 3.1.0

* Support `JournalSizeLimit` and `PersistWal` connection string options.

## 3.0.0

* Support [URI filenames](https://www.sqlite.org/uri.html).
* Supported frameworks: `netstandard2.0`, `net472`, `xamarin.ios10`, `monoandroid81`

## 2.13.1

* Add `net47` target platform.

## 2.13.0

* Rewrite `SQLiteConnection.StatementCompleted` to use `sqlite3_trace_v2` API internally: [#29](https://github.com/Faithlife/System.Data.SQLite/pull/29).
  * This requires [SQLite 3.14](https://sqlite.org/releaselog/3_14.html) or later.

## 2.12.3

* Use `DateTimeStyles.AdjustToUniversal`: [#24](https://github.com/Faithlife/System.Data.SQLite/pull/24).

## 2.12.2

* Create more informative error messages in `SQLiteException`.

## 2.12.1

* Fix the native interop signature for `sqlite3_interrupt`.

## 2.12.0

* Sleep in native code when the database is busy. This reduces managed/native interop.
