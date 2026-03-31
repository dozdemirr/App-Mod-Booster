#!/usr/bin/env python3
"""
Execute SQL script on Azure SQL Database using Azure Active Directory authentication
"""
import pyodbc
import struct
import os
from azure.identity import AzureCliCredential

# Database connection settings - use env var or default
SERVER = os.environ.get('SQL_SERVER_FQDN', 'example.database.windows.net')
DATABASE = "Northwind"
SQL_SCRIPT_FILE = "Database-Schema/database_schema.sql"

def get_access_token():
    credential = AzureCliCredential()
    token = credential.get_token("https://database.windows.net/.default")
    return token.token

def execute_sql_script(server, database, script_file):
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
        print(f"Reading SQL script from {script_file}...")
        with open(script_file, 'r') as f:
            sql_script = f.read()
        statements = []
        current_statement = []
        for line in sql_script.split('\n'):
            line = line.strip()
            if line.upper() == 'GO':
                if current_statement:
                    statements.append('\n'.join(current_statement))
                    current_statement = []
            elif line:
                current_statement.append(line)
        if current_statement:
            statements.append('\n'.join(current_statement))
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
        print("\n✓ All SQL statements executed successfully!")
    finally:
        conn.close()

if __name__ == "__main__":
    try:
        execute_sql_script(SERVER, DATABASE, SQL_SCRIPT_FILE)
    except Exception as e:
        print(f"\n✗ Error: {e}")
        exit(1)
