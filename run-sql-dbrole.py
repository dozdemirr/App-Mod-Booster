#!/usr/bin/env python3
"""
run-sql-dbrole.py
Grants the user-assigned managed identity database roles on Azure SQL.
Replaces MANAGED-IDENTITY-NAME placeholder in script.sql with the actual identity name.
Works on macOS and Linux (cross-platform sed replacement handled in Python).
"""
import pyodbc
import struct
import os
import sys
import shutil
from azure.identity import AzureCliCredential

# Database connection settings
SERVER = "example.database.windows.net"
DATABASE = "Northwind"
SQL_SCRIPT_FILE = "script.sql"


def get_access_token():
    """Get Azure AD access token using Azure CLI credentials"""
    credential = AzureCliCredential()
    token = credential.get_token("https://database.windows.net/.default")
    return token.token


def execute_sql_script(server, database, script_content):
    """Execute SQL script string using Azure AD token authentication"""

    print("Getting Azure AD access token...")
    access_token = get_access_token()

    token_bytes = access_token.encode("utf-16-le")
    token_struct = struct.pack(f"<I{len(token_bytes)}s", len(token_bytes), token_bytes)

    connection_string = (
        f"Driver={{ODBC Driver 18 for SQL Server}};"
        f"Server={server};"
        f"Database={database};"
        f"Encrypt=yes;"
        f"TrustServerCertificate=no;"
    )

    SQL_COPT_SS_ACCESS_TOKEN = 1256

    print(f"Connecting to {server}/{database}...")
    conn = pyodbc.connect(connection_string, attrs_before={SQL_COPT_SS_ACCESS_TOKEN: token_struct})

    try:
        # Split on GO statements
        statements = []
        current_statement = []

        for line in script_content.split("\n"):
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
        for i, statement in enumerate(statements, 1):
            if statement.strip():
                print(f"Executing statement {i}/{len(statements)}...")
                try:
                    cursor.execute(statement)
                    conn.commit()
                    print(f"  ✓ Statement {i} executed successfully")
                except Exception as e:
                    print(f"  ✗ Error executing statement {i}: {e}")
                    raise

        print("\n✓ All SQL role statements executed successfully!")

    finally:
        conn.close()


if __name__ == "__main__":
    # Allow overrides from environment variables
    server = os.environ.get("SQL_SERVER", SERVER)
    database = os.environ.get("SQL_DATABASE", DATABASE)
    managed_identity_name = os.environ.get("MANAGED_IDENTITY_NAME", "mid-AppModAssist-16-10-15")

    print(f"Using managed identity: {managed_identity_name}")

    # Cross-platform replace: read file, substitute placeholder, use in memory
    # (avoids non-portable sed -i behaviour differences between macOS and Linux)
    try:
        with open(SQL_SCRIPT_FILE, "r") as f:
            sql_content = f.read()
    except FileNotFoundError:
        print(f"✗ Could not find {SQL_SCRIPT_FILE}")
        sys.exit(1)

    sql_content = sql_content.replace("MANAGED-IDENTITY-NAME", managed_identity_name)

    try:
        execute_sql_script(server, database, sql_content)
    except Exception as e:
        print(f"\n✗ Error: {e}")
        sys.exit(1)
