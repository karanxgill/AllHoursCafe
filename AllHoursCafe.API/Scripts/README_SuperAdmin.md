# SuperAdmin Role

## Overview
The SuperAdmin role is a special administrative role with elevated privileges in the All Hours Cafe application. SuperAdmins have all the capabilities of regular Admins, plus additional permissions to manage other SuperAdmins.

## Special Privileges

1. **User Management**:
   - Only SuperAdmins can create other SuperAdmins
   - Only SuperAdmins can demote other SuperAdmins to Admin or User roles
   - Only SuperAdmins can disable SuperAdmin accounts

2. **Security**:
   - Regular Admins cannot disable SuperAdmin accounts
   - Regular Admins cannot change a SuperAdmin's role

## Creating a SuperAdmin

To create the initial SuperAdmin user, you can:

1. Run the `CreateSuperAdmin.sql` script in this directory against your database
2. Use the default credentials:
   - Email: superadmin@allhourscafe.com
   - Password: SuperAdmin123!

**IMPORTANT**: Change the default password immediately after first login!

## Best Practices

1. Limit the number of SuperAdmin accounts to only those who absolutely need this level of access
2. Regularly audit the list of SuperAdmin users
3. Use strong, unique passwords for SuperAdmin accounts
4. Consider implementing additional security measures for SuperAdmin accounts (e.g., 2FA)

## Technical Implementation

The SuperAdmin role is implemented with special checks in the following areas:

1. `AdminController.ToggleUserStatus` - Prevents non-SuperAdmins from disabling SuperAdmin accounts
2. `AdminController.UpdateRole` - Prevents non-SuperAdmins from creating or demoting SuperAdmin accounts
3. User interface elements that hide certain actions when the current user doesn't have sufficient permissions
