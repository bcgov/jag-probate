<template>
  <div id="status" class="card bg-white border-white" style="min-height: 784px;">
    <div class="card home-content border-white">
      <!-- Header Section -->
      <div name="info-content-header" class="header-section">
        <h1 class="page-title">Previous Activity</h1>
        <div class="card bg-white border-white">
          <p class="instruction-text">
            To resume a previous session, click the Resume button next to the activity. 
            To start a new session, click the Begin New Session button at the bottom of the page.
          </p>
        </div>
        <div class="header-divider"></div>
      </div>

      <!-- Table Section -->
      <div style="height: 525px;">
        <div class="card bg-white border-white" style="height: 525px;">
          <div class="mx-0 b-table-sticky-header table-responsive-sm" style="max-height: 600px; overflow-y: auto;">
            <table 
              role="table" 
              aria-busy="false" 
              aria-colcount="6" 
              class="table b-table table-striped table-borderless table-sm"
            >
              <thead role="rowgroup" class="thead-dark">
                <tr role="row">
                  <th 
                    role="columnheader" 
                    scope="col" 
                    tabindex="0" 
                    aria-colindex="1" 
                    class="b-table-sort-icon-left" 
                    style="font-size: 11pt; width: 20%;"
                  >
                    <div>Application</div>
                    <span class="sr-only"> (Click to sort Ascending)</span>
                  </th>
                  <th 
                    role="columnheader" 
                    scope="col" 
                    tabindex="0" 
                    aria-colindex="2" 
                    aria-sort="descending"
                    class="b-table-sort-icon-left" 
                    style="font-size: 11pt; width: 20%;"
                  >
                    <div>Last Updated</div>
                    <span class="sr-only"> (Click to sort Ascending)</span>
                  </th>
                  <th 
                    role="columnheader" 
                    scope="col" 
                    tabindex="0" 
                    aria-colindex="3" 
                    class="b-table-sort-icon-left" 
                    style="font-size: 11pt; width: 20%;"
                  >
                    <div>Last Filed</div>
                    <span class="sr-only"> (Click to sort Ascending)</span>
                  </th>
                  <th 
                    role="columnheader" 
                    scope="col" 
                    tabindex="0" 
                    aria-colindex="4" 
                    class="b-table-sort-icon-left" 
                    style="font-size: 10pt; width: 15%;"
                  >
                    <div>Status</div>
                    <span class="sr-only"> (Click to sort Ascending)</span>
                  </th>
                  <th 
                    role="columnheader" 
                    scope="col" 
                    tabindex="0" 
                    aria-colindex="5" 
                    style="font-size: 10pt; width: 15%;"
                  >
                    <div>Package#</div>
                    <span class="sr-only"> (Click to clear sorting)</span>
                  </th>
                  <th 
                    role="columnheader" 
                    scope="col" 
                    tabindex="0" 
                    aria-colindex="6" 
                    aria-label="Edit"
                    style="font-size: 10pt; width: 10%;"
                  >
                    <div></div>
                    <span class="sr-only"> (Click to clear sorting)</span>
                  </th>
                </tr>
              </thead>
              <tbody role="rowgroup">
                <tr v-for="caseItem in cases" :key="caseItem.id" role="row">
                  <td aria-colindex="1" role="cell" class="border-top">
                    <span>{{ caseItem.title }}</span>
                  </td>
                  <td aria-colindex="2" role="cell" class="border-top">
                    <span>{{ formatDateTime(caseItem.filedDate) }}</span>
                  </td>
                  <td aria-colindex="3" role="cell" class="border-top">
                    <span></span>
                  </td>
                  <td aria-colindex="4" role="cell" class="border-top">
                    {{ caseItem.status }}
                  </td>
                  <td aria-colindex="5" role="cell" class="border-top">
                    {{ caseItem.caseNumber }}
                  </td>
                  <td aria-colindex="6" role="cell" class="border-top">
                    <button 
                      title="Remove Application" 
                      type="button" 
                      class="btn my-0 py-0 border-0 btn-transparent btn-sm"
                      @click="removeCase(caseItem.id)"
                    >
                      <svg 
                        viewBox="0 0 16 16" 
                        width="1em" 
                        height="1em" 
                        focusable="false" 
                        role="img" 
                        aria-label="trash fill" 
                        xmlns="http://www.w3.org/2000/svg" 
                        fill="currentColor" 
                        class="bi-trash-fill b-icon bi text-danger" 
                        style="font-size: 125%;"
                      >
                        <g>
                          <path 
                            fill-rule="evenodd" 
                            d="M2.5 1a1 1 0 0 0-1 1v1a1 1 0 0 0 1 1H3v9a2 2 0 0 0 2 2h6a2 2 0 0 0 2-2V4h.5a1 1 0 0 0 1-1V2a1 1 0 0 0-1-1H10a1 1 0 0 0-1-1H7a1 1 0 0 0-1 1H2.5zm3 4a.5.5 0 0 1 .5.5v7a.5.5 0 0 1-1 0v-7a.5.5 0 0 1 .5-.5zM8 5a.5.5 0 0 1 .5.5v7a.5.5 0 0 1-1 0v-7A.5.5 0 0 1 8 5zm3 .5a.5.5 0 0 0-1 0v7a.5.5 0 0 0 1 0v-7z"
                          ></path>
                        </g>
                      </svg>
                    </button>
                    <button 
                      title="Resume Application" 
                      type="button" 
                      class="btn my-0 py-0 border-0 btn-transparent btn-sm"
                      @click="resumeCase(caseItem.id)"
                    >
                      <svg 
                        viewBox="0 0 16 16" 
                        width="1em" 
                        height="1em" 
                        focusable="false" 
                        role="img" 
                        aria-label="pencil square" 
                        xmlns="http://www.w3.org/2000/svg" 
                        fill="currentColor" 
                        class="bi-pencil-square b-icon bi text-primary" 
                        style="font-size: 125%;"
                      >
                        <g>
                          <path 
                            d="M15.502 1.94a.5.5 0 0 1 0 .706L14.459 3.69l-2-2L13.502.646a.5.5 0 0 1 .707 0l1.293 1.293zm-1.75 2.456l-2-2L4.939 9.21a.5.5 0 0 0-.121.196l-.805 2.414a.25.25 0 0 0 .316.316l2.414-.805a.5.5 0 0 0 .196-.12l6.813-6.814z"
                          ></path>
                          <path 
                            fill-rule="evenodd" 
                            d="M1 13.5A1.5 1.5 0 0 0 2.5 15h11a1.5 1.5 0 0 0 1.5-1.5v-6a.5.5 0 0 0-1 0v6a.5.5 0 0 1-.5.5h-11a.5.5 0 0 1-.5-.5v-11a.5.5 0 0 1 .5-.5H9a.5.5 0 0 0 0-1H2.5A1.5 1.5 0 0 0 1 2.5v11z"
                          ></path>
                        </g>
                      </svg>
                    </button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <!-- Button Menu Section -->
      <div name="button-menu" class="card button-content bg-white border-white">
        <div class="card-body p-3">
          <div class="button-row">
            <button 
              type="button" 
              class="btn btn-success begin-session-btn"
              @click="createCase"
            >
              Begin NEW Session
            </button>
            <a class="terms-link" @click="showTerms">
              Terms and Conditions
            </a>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
  import { ref, onMounted } from 'vue';
  import { useRouter } from 'vue-router';
  import axios from 'axios';

  interface Case {
    id: number;
    caseNumber: string;
    title: string;
    status: string;
    filedDate: string;
    description: string;
  }

  const router = useRouter();
  const cases = ref<Case[]>([]);
  const loading = ref(true);
  const error = ref('');

  const fetchCases = async () => {
    try {
      loading.value = true;
      const response = await axios.get('/api/cases');
      cases.value = response.data;
    } catch (err: any) {
      error.value = err.response?.data?.message || 'Failed to load cases';
    } finally {
      loading.value = false;
    }
  };

  const createCase = () => {
    router.push('/cases/new');
  };

  const resumeCase = (id: number) => {
    router.push(`/cases/${id}`);
  };

  const removeCase = async (id: number) => {
    if (confirm('Are you sure you want to remove this application?')) {
      try {
        await axios.delete(`/api/cases/${id}`);
        await fetchCases();
      } catch (err: any) {
        alert(err.response?.data?.message || 'Failed to remove case');
      }
    }
  };

  const showTerms = () => {
    // Implement terms and conditions modal/page
    alert('Terms and Conditions');
  };

  const formatDateTime = (date: string) => {
    if (!date) return '';
    const d = new Date(date);
    const options: Intl.DateTimeFormatOptions = { 
      weekday: 'short', 
      year: 'numeric', 
      month: 'short', 
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      hour12: false
    };
    return d.toLocaleString('en-US', options).replace(',', '');
  };

  onMounted(() => {
    fetchCases();
  });
</script>

<style scoped>
  #status {
    margin: 0;
    padding: 0;
  }

  .card {
    border-radius: 0;
    border: none;
  }

  .home-content {
    padding: 2rem 6rem;
    max-width: 100%;
  }

  .header-section {
    padding: 0 0 1rem 0;
    margin: 0;
  }

  .page-title {
    font-size: 1.75rem;
    font-weight: 600;
    margin-bottom: 0.75rem;
    color: #333;
  }

  .instruction-text {
    margin: 0.5rem 0;
    padding: 0;
    font-size: 0.95rem;
    color: #555;
    line-height: 1.5;
  }

  .header-divider {
    margin: 1rem 0;
    border-top: 1px solid #dee2e6;
  }

  .b-table-sticky-header {
    border: none;
    margin: 0;
    padding: 0;
  }

  .table {
    margin-bottom: 0;
    font-size: 0.9rem;
  }

  .table thead th {
    border-bottom: none;
    position: sticky;
    top: 0;
    z-index: 10;
    padding: 0.75rem 0.5rem;
  }

  .table tbody td {
    padding: 0.75rem 0.5rem;
    vertical-align: middle;
  }

  .btn-transparent {
    background-color: transparent;
    padding: 0.25rem 0.5rem;
  }

  .btn-transparent:hover {
    background-color: rgba(0, 0, 0, 0.05);
  }

  .button-content {
    margin-top: 2rem;
    padding: 0;
  }

  .button-row {
    display: flex;
    align-items: center;
    justify-content: flex-end;
    gap: 2rem;
    padding: 1rem 0;
  }

  .begin-session-btn {
    padding: 0.5rem 2rem;
    font-size: 1rem;
    font-weight: 500;
    background-color: #5a8f5a;
    border: none;
    border-radius: 4px;
  }

  .begin-session-btn:hover {
    background-color: #4a7a4a;
  }

  .terms-link {
    color: #007bff;
    text-decoration: underline;
    cursor: pointer;
    font-size: 0.95rem;
  }

  .terms-link:hover {
    color: #0056b3;
    text-decoration: underline;
  }

  .border-gray {
    border-color: #dee2e6 !important;
  }

  .sr-only {
    position: absolute;
    width: 1px;
    height: 1px;
    padding: 0;
    margin: -1px;
    overflow: hidden;
    clip: rect(0, 0, 0, 0);
    white-space: nowrap;
    border: 0;
  }
</style>
