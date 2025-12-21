<template>
  <div class="home">
    <div class="jumbotron">
      <h1 class="display-4">Welcome to Probate Management System</h1>
      <p class="lead">Manage probate cases efficiently and securely.</p>
      <hr class="my-4" />
      <p>Get started by viewing cases or exploring the system.</p>
      <router-link to="/cases" class="btn btn-primary btn-lg">View Cases</router-link>
    </div>

    <div class="row mt-5">
      <div class="col-md-4">
        <div class="card">
          <div class="card-body">
            <h5 class="card-title">Case Management</h5>
            <p class="card-text">Create, update, and track probate cases through their lifecycle.</p>
            <router-link to="/cases" class="btn btn-outline-primary">Manage Cases</router-link>
          </div>
        </div>
      </div>
      <div class="col-md-4">
        <div class="card">
          <div class="card-body">
            <h5 class="card-title">Document Storage</h5>
            <p class="card-text">Securely store and manage case-related documents.</p>
            <a href="#" class="btn btn-outline-primary">View Documents</a>
          </div>
        </div>
      </div>
      <div class="col-md-4">
        <div class="card">
          <div class="card-body">
            <h5 class="card-title">Report</h5>
            <p class="card-text">Generate comprehensive reports and analytics.</p>
            <a href="#" class="btn btn-outline-primary">View Reports</a>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
// Home view component
</script>

<style scoped>
.jumbotron {
  background-color: #f8f9fa;
  padding: 2rem;
  border-radius: 0.3rem;
}

.card {
  height: 100%;
}
</style>
