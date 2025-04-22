# AllHoursCafe Tools

This directory contains utility tools for the AllHoursCafe application.

## CreateSuperAdmin

This tool checks if a SuperAdmin user exists in the database and creates one if it doesn't.

### Usage

```bash
# Navigate to the Tools directory
cd AllHoursCafe/AllHoursCafe.API/Tools

# Build the tool
dotnet build CreateSuperAdmin.csproj

# Run the tool with default connection string
dotnet run --project CreateSuperAdmin.csproj

# Or run with a custom connection string
dotnet run --project CreateSuperAdmin.csproj "server=myserver;port=3306;database=mydb;user=myuser;password=mypassword"
```

### Default SuperAdmin Credentials

- **Email**: superadmin@allhourscafe.com
- **Password**: SuperAdmin@123

**IMPORTANT**: Change the default password after first login!

### What This Tool Does

1. Connects to the database using the provided connection string
2. Checks if a user with the "SuperAdmin" role exists
3. If no SuperAdmin exists, creates one with the default credentials
4. Outputs the result of the operation

### When to Use This Tool

- After initial database setup
- When you need to ensure a SuperAdmin user exists
- When you've accidentally deleted all SuperAdmin users and need to restore access
