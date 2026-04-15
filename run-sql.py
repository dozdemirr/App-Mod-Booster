#!/usr/bin/env python3
"""Execute SQL schema script on Azure SQL with Azure AD auth."""
import pyodbc
import struct
from azure.identity import AzureCliCredential

SERVER = "example.database.windows.net"
DATABASE = "northwind"
SQL_SCRIPT_FILE = "Database-Schema/database_schema.sql"


def get_access_token():
    credential = AzureCliCredential()
    return credential.get_token("https://database.windows.net/.default").token


def execute_sql_script(server, database, script_file):
    token = get_access_token()
    token_bytes = token.encode("utf-16-le")
    token_struct = struct.pack(f"<I{len(token_bytes)}s", len(token_bytes), token_bytes)

    connection_string = (
        "Driver={ODBC Driver 18 for SQL Server};"
        f"Server={server};"
        f"Database={database};"
        "Encrypt=yes;TrustServerCertificate=no;"
    )

    conn = pyodbc.connect(connection_string, attrs_before={1256: token_struct})
    try:
        with open(script_file, "r", encoding="utf-8") as f:
            sql_script = f.read()

        statements = []
        current = []
        for line in sql_script.split("\n"):
            check = line.strip()
            if check.upper() == "GO":
                if current:
                    statements.append("\n".join(current))
                    current = []
            elif check:
                current.append(line)
        if current:
            statements.append("\n".join(current))

        cursor = conn.cursor()
        for statement in statements:
            if statement.strip():
                cursor.execute(statement)
                conn.commit()
    finally:
        conn.close()


if __name__ == "__main__":
    execute_sql_script(SERVER, DATABASE, SQL_SCRIPT_FILE)
