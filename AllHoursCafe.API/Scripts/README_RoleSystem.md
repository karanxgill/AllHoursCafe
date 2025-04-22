# All Hours Cafe Role System

## Overview
The All Hours Cafe application implements a hierarchical role system with three levels:
1. **SuperAdmin** - Highest level with full administrative privileges
2. **Admin** - Regular administrators with limited privileges
3. **User** - Regular customers with no administrative access

## Role Hierarchy and Permissions

### SuperAdmin
- Created automatically as the first admin in the system
- Can create, edit, and delete all content
- Can create and manage other SuperAdmins
- Can create and manage regular Admins
- Can disable any user account, including other SuperAdmin accounts
- Has exclusive access to the role management functionality

### Admin
- Created only by SuperAdmins
- Can manage content (menu items, categories, orders, etc.)
- Cannot create or manage other Admins
- Cannot disable any user accounts
- Cannot access role management functionality

### User
- Regular customers who register through the signup process
- Can place orders, manage their profile, etc.
- No administrative access

## Implementation Details

### First Admin Rule
- The first admin user created in the system is automatically assigned the SuperAdmin role
- This is handled by the `DbSeeder` class during initial database setup
- Default SuperAdmin credentials:
  - Email: superadmin@allhourscafe.com
  - Password: SuperAdmin@123

### Role Management Restrictions
- The "Update Role" button is only visible to SuperAdmins
- The `UpdateRole` action in the `AdminController` is protected with `[Authorize(Roles = "SuperAdmin")]`
- Regular Admins cannot see or access role management functionality

### User Status Management
- Only SuperAdmins can enable/disable user accounts
- Only SuperAdmins can disable other SuperAdmin accounts
- Regular Admins cannot disable any user accounts
- This is enforced in the `ToggleUserStatus` action in the `AdminController` with the `[Authorize(Roles = "SuperAdmin")]` attribute

## Security Considerations
- SuperAdmin accounts should be limited to a small number of trusted individuals
- The default SuperAdmin password should be changed immediately after first login
- Regular audit of user roles should be performed to ensure proper access control

## Technical Implementation
The role system is implemented through:
1. The `Role` property in the `User` model
2. Authorization attributes on controller actions
3. Conditional rendering in views based on the current user's role
4. Server-side validation in controller actions to enforce role-based permissions
