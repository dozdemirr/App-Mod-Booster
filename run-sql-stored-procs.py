#!/usr/bin/env python3
"""
Execute SQL script on Azure SQL Database using Azure Active Directory authentication
"""
import pyodbc
import struct
import os
from azure.identity import AzureCliCredential

SERVER = os.getenv("SQL_SERVER_FQDN", "example.database.windows.net")
DATABASE = os.getenv("SQL_DATABASE", "northwind")
SQL_SCRIPT_FILE = "Database-Schema/stored-procedures.sql"


def get_access_token():
    credential = AzureCliCredential()
    token = credential.get_token("https://database.windows.net/.default")
    return token.token


def execute_sql_script(server, database, script_file):
    access_token = get_access_token()
    token_bytes = access_token.encode("utf-16-le")
    token_struct = struct.pack(f"<I{len(token_bytes)}s", len(token_bytes), token_bytes)
    connection_string = (
        f"Driver={{ODBC Driver 18 for SQL Server}};"
        f"Server={server};"
        f"Database={database};"
        "Encrypt=yes;"
        "TrustServerCertificate=no;"
    )
    conn = pyodbc.connect(connection_string, attrs_before={1256: token_struct})
    try:
        with open(script_file, "r", encoding="utf-8") as sql_file:
            sql_script = sql_file.read()

        statements = []
        current_statement = []
        for line in sql_script.split("\n"):
            stripped = line.strip()
            if stripped.upper() == "GO":
                if current_statement:
                    statements.append("\n".join(current_statement))
                    current_statement = []
            elif stripped:
                current_statement.append(line)
        if current_statement:
            statements.append("\n".join(current_statement))

        cursor = conn.cursor()
        for statement in statements:
            cursor.execute(statement)
            conn.commit()
    finally:
        conn.close()


if __name__ == "__main__":
    execute_sql_script(SERVER, DATABASE, SQL_SCRIPT_FILE)
