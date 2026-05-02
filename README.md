# 💰 Claims Management System (SaaS)

## 📌 Overview

A multi-tenant SaaS platform designed to manage organizational claims, approvals, supporting documents, payments, and audit trails.

The system is suitable for NGOs, schools, churches, government departments, and corporate environments where structured claim workflows are required.

---

## 🎯 Purpose

To digitize and automate claim processes including:

* Submission
* Approval workflows
* Payment tracking
* Audit and compliance

---

## 👥 Target Users

* NGOs
* Schools
* Churches
* Government departments
* Field teams
* Corporate finance departments

---

## 🚀 Core Features

### 🧾 Claim Types

* Travel Claims
* Subsistence Claims
* Fuel Claims
* Medical Claims
* Procurement Reimbursements
* Project Expense Claims
* Allowance Claims

---

### 📥 Claim Submission

* Dynamic claim forms
* Multi-line claim entries
* Receipt/document uploads
* Currency support (USD/ZiG)
* Mileage & per diem calculation
* Bank details capture

---

### 🔄 Approval Workflow

* Supervisor review
* Finance review
* Accounts approval
* Final approval
* Rejection with reason
* Return for correction
* Multi-level configurable workflow

---

### 💳 Payment Tracking

* Payment batches
* Paid/unpaid status
* Payment references
* Bank export schedules
* Excel export

---

### 📊 Reports

* Claims by status
* Claims by department
* Claims by project
* Aging reports
* Paid vs outstanding claims

---

### 🔍 Audit & Compliance

* Full audit trail
* Approval history
* User activity tracking
* Field-level change tracking

---

## 🏗️ System Components

* Claim Management Service
* Approval Workflow Engine
* Payment Processing Module
* Reporting Engine
* Audit Logging Service
* Notification System

---

## 🛠️ Tech Stack

* ASP.NET Core Web API
* Entity Framework Core
* SQL Server
* ASP.NET Identity
* React + TypeScript
* Hangfire (background jobs)
* ClosedXML (Excel)
* QuestPDF (PDF reports)
* Serilog (logging)

---

## 📂 Architecture

* Clean Architecture
* Modular Monolith
* Multi-Tenant Ready

---

## 📌 Key Models

* Claim
* ClaimLine
* ClaimAttachment
* ClaimApproval
* ClaimStatusHistory
* PaymentBatch
* ClaimPayment
* Currency
* ExchangeRate
* AuditLog
* Notification

---

## 🔥 Why This Project Matters

This system demonstrates:

* Financial workflow automation
* Approval engine design
* Enterprise audit and compliance
* Multi-step business processes

---

## 🚀 Future Enhancements

* Mobile claims submission
* Integration with accounting systems
* OCR receipt scanning
* AI fraud detection
* Payment gateway integration

---

## 👨‍💻 Author

Built as an enterprise-grade financial workflow system.
