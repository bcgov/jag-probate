<template>
  <div id="status" class="card bg-white border-white previous-activity">
    <div class="card home-content border-white p-0">
      <!-- Error Banner -->
      <div class="alert alert-danger mt-4" v-if="error">{{ error }}</div>

      <!-- Header Section -->
      <div name="info-content-header" class="pt-5">
        <h1>Previous Activity</h1>
        <div class="card bg-white border-white">
          <p class="ml-0 mb-1">
            To resume a previous session, click the Resume button next to the
            activity. To start a new session, click the Begin New Session button
            at the bottom of the page.
          </p>
        </div>
        <div
          class="mb-4 border border-gray border-right-0 border-left-0 border-bottom-0"
        ></div>
      </div>

      <!-- Table Section -->
      <div v-if="dataLoaded" class="table-section">
        <!-- Empty State -->
        <div
          class="card bg-white border-white"
          v-if="!previousApplications.length"
        >
          <span class="text-muted ml-4 mb-5">No previous applications.</span>
        </div>

        <!-- Applications Table -->
        <div v-else class="card bg-white border-white table-section">
          <div
            class="mx-0 b-table-sticky-header table-responsive-sm"
            style="max-height: 600px; overflow-y: auto"
          >
            <table
              role="table"
              class="table b-table table-striped table-borderless table-sm"
            >
              <thead role="rowgroup">
                <tr role="row">
                  <th
                    style="font-size: 11pt; width: 20%; cursor: pointer"
                    @click="setSort('deceased_name')"
                  >
                    <SortIcon
                      column="deceased_name"
                      :sort-key="sortKey"
                      :sort-desc="sortDesc"
                    />Application
                  </th>
                  <th
                    style="font-size: 11pt; width: 20%; cursor: pointer"
                    @click="setSort('lastUpdated')"
                  >
                    <SortIcon
                      column="lastUpdated"
                      :sort-key="sortKey"
                      :sort-desc="sortDesc"
                    />Last Updated
                  </th>
                  <th
                    style="font-size: 11pt; width: 20%; cursor: pointer"
                    @click="setSort('lastFiled')"
                  >
                    <SortIcon
                      column="lastFiled"
                      :sort-key="sortKey"
                      :sort-desc="sortDesc"
                    />Last Filed
                  </th>
                  <th
                    style="font-size: 10pt; width: 15%; cursor: pointer"
                    @click="setSort('status')"
                  >
                    <SortIcon
                      column="status"
                      :sort-key="sortKey"
                      :sort-desc="sortDesc"
                    />Status
                  </th>
                  <th style="font-size: 10pt; width: 15%">Package#</th>
                  <th style="font-size: 10pt; width: 10%"></th>
                </tr>
              </thead>
              <tbody role="rowgroup">
                <tr v-for="app in sortedApplications" :key="app.id" role="row">
                  <td class="border-top">
                    {{ formatFullName(app.deceased_name) }}
                  </td>
                  <td class="border-top">
                    {{ beautifyDate(app.lastUpdatedDate) }}
                  </td>
                  <td class="border-top">
                    {{ beautifyDate(app.lastFiledDate ?? app.createdAt) }}
                  </td>
                  <td class="border-top">{{ app.status }}</td>
                  <td class="border-top">{{ app.chefsSubmissionId }}</td>
                  <td class="border-top">
                    <!-- Trash: only shown when application has never been filed -->
                    <button
                      v-if="app.lastFiled === 0"
                      title="Remove Application"
                      type="button"
                      class="btn my-0 py-0 border-0 btn-transparent btn-sm"
                      @click="removeApplication(app)"
                    >
                      <svg
                        viewBox="0 0 16 16"
                        width="1.25em"
                        height="1.25em"
                        fill="currentColor"
                        class="text-danger"
                        xmlns="http://www.w3.org/2000/svg"
                      >
                        <path
                          fill-rule="evenodd"
                          d="M2.5 1a1 1 0 0 0-1 1v1a1 1 0 0 0 1 1H3v9a2 2 0 0 0 2 2h6a2 2 0 0 0 2-2V4h.5a1 1 0 0 0 1-1V2a1 1 0 0 0-1-1H10a1 1 0 0 0-1-1H7a1 1 0 0 0-1 1H2.5zm3 4a.5.5 0 0 1 .5.5v7a.5.5 0 0 1-1 0v-7a.5.5 0 0 1 .5-.5zM8 5a.5.5 0 0 1 .5.5v7a.5.5 0 0 1-1 0v-7A.5.5 0 0 1 8 5zm3 .5a.5.5 0 0 0-1 0v7a.5.5 0 0 0 1 0v-7z"
                        />
                      </svg>
                    </button>

                    <!-- Resume (pencil) -->
                    <button
                      title="Resume Application"
                      type="button"
                      class="btn my-0 py-0 border-0 btn-transparent btn-sm"
                      @click="resumeApplication(app)"
                    >
                      <svg
                        viewBox="0 0 16 16"
                        width="1.25em"
                        height="1.25em"
                        fill="currentColor"
                        class="text-primary"
                        xmlns="http://www.w3.org/2000/svg"
                      >
                        <path
                          d="M15.502 1.94a.5.5 0 0 1 0 .706L14.459 3.69l-2-2L13.502.646a.5.5 0 0 1 .707 0l1.293 1.293zm-1.75 2.456l-2-2L4.939 9.21a.5.5 0 0 0-.121.196l-.805 2.414a.25.25 0 0 0 .316.316l2.414-.805a.5.5 0 0 0 .196-.12l6.813-6.814z"
                        />
                        <path
                          fill-rule="evenodd"
                          d="M1 13.5A1.5 1.5 0 0 0 2.5 15h11a1.5 1.5 0 0 0 1.5-1.5v-6a.5.5 0 0 0-1 0v6a.5.5 0 0 1-.5.5h-11a.5.5 0 0 1-.5-.5v-11a.5.5 0 0 1 .5-.5H9a.5.5 0 0 0 0-1H2.5A1.5 1.5 0 0 0 1 2.5v11z"
                        />
                      </svg>
                    </button>

                    <!-- Paper plane: only shown when application has been filed -->
                    <button
                      v-if="app.lastFiled !== 0"
                      title="Navigate To Submitted Application"
                      type="button"
                      class="btn my-0 py-0 border-0 btn-transparent btn-sm"
                      @click="navigateToEFilingHub(app.id)"
                    >
                      <span class="fa fa-paper-plane text-info" />
                    </button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>

    <div name="button-menu" class="card button-content border-white bg-white">
      <div class="card-body">
        <p class="process-map-intro-text">
          <strong
            >The Process Map provides a general overview for a Representation
            Grant.</strong
          >
        </p>
        <div class="d-flex align-items-center justify-content-between">
          <button
            type="button"
            class="btn btn-success process-map-button"
            @click="showProcessMap = true"
            aria-haspopup="dialog"
          >
            View/Print Process Map
            <span class="fa fa-print ms-1" aria-hidden="true"></span>
          </button>
          <div class="d-flex align-items-center gap-3">
            <button
              type="button"
              class="btn btn-success application-button"
              @click="createCase"
            >
              Begin NEW Session
            </button>
          </div>
        </div>
      </div>
    </div>
    <!-- Delete Confirmation Modal -->
    <div v-if="confirmDelete" class="modal d-block" tabindex="-1" role="dialog">
      <div class="modal-dialog" role="document">
        <div class="modal-content">
          <div class="modal-header bg-warning text-light">
            <h2 class="modal-title mb-0 text-light">
              Confirm Delete Application
            </h2>
            <button
              type="button"
              class="btn btn-outline-warning text-light"
              @click="confirmDelete = false"
            >
              &times;
            </button>
          </div>
          <div class="modal-body">
            <div v-if="deleteError" class="mb-3">
              <span
                class="badge bg-danger text-white p-2 delete-error-badge"
                :title="deleteErrorMsgDesc"
              >
                {{ deleteErrorMsg }}
                <button
                  type="button"
                  class="btn-close btn-close-white ms-3"
                  @click="deleteError = false"
                />
              </span>
            </div>
            <h4>
              Are you sure you want to delete your
              <b>"{{ applicationToDelete?.app_type }}"</b> application?
            </h4>
          </div>
          <div class="modal-footer">
            <button
              type="button"
              class="btn btn-danger"
              @click="confirmRemoveApplication"
            >
              Confirm
            </button>
            <button
              type="button"
              class="btn btn-primary"
              @click="confirmDelete = false"
            >
              Cancel
            </button>
          </div>
        </div>
      </div>
    </div>
    <div v-if="confirmDelete" class="modal-backdrop fade show"></div>
  </div>
</template>

<script setup lang="ts">
  //import { useApplicationStore } from '@/stores/PreviousApplicationStore';
  import ChefsService from '@/services/ChefsService';
  import moment from 'moment-timezone';
  import { inject, onMounted, onUnmounted, computed, ref } from 'vue';
  import { useRouter } from 'vue-router';
  import SortIcon from '@/components/shared/SortIcon.vue';

  import '../styles/PreviousActivity.styles.css';

  const chefsService = inject<ChefsService>('chefsService')!;

  // ── Router & Store ────────────────────────────────────────────────────────────
  const router = useRouter();

  // ── State ─────────────────────────────────────────────────────────────────────
  const previousApplications = ref<any[]>([]);
  const dataLoaded = ref(false);
  const error = ref('');

  const confirmDelete = ref(false);
  const applicationToDelete = ref<any>({});
  const deleteError = ref(false);
  const deleteErrorMsg = ref('');
  const deleteErrorMsgDesc = ref('');

  const sortKey = ref<string>('lastUpdated');
  const sortDesc = ref<boolean>(true);

  const sortedApplications = computed(() => {
    if (!sortKey.value) return previousApplications.value;

    return [...previousApplications.value].sort((a, b) => {
      const valA = a[sortKey.value];
      const valB = b[sortKey.value];

      let result = 0;
      if (valA == null) result = -1;
      else if (valB == null) result = 1;
      else if (typeof valA === 'string') result = valA.localeCompare(valB);
      else result = valA - valB;

      return sortDesc.value ? -result : result;
    });
  });

  function setSort(key: string) {
    if (sortKey.value === key) {
      sortDesc.value = !sortDesc.value;
    } else {
      sortKey.value = key;
      sortDesc.value = false;
    }
  }

  // ── Helpers ───────────────────────────────────────────────────────────────────
  function formatFullName(name: any): string {
    if (!name) return '';
    if (typeof name === 'string') return name;
    const { first = '', middle = '', last = '' } = name;
    return [first, middle, last].filter(Boolean).join(' ');
  }

  function beautifyDate(dateStr: string): string {
    if (!dateStr) return '';
    return moment(dateStr)
      .tz('America/Vancouver')
      .format('ddd, MMM DD YYYY HH:mm');
  }

  // ── Navigation ────────────────────────────────────────────────────────────────
  function navigateToEFilingHub(id: number) {
    console.log('going to hub', id);
    // TODO: replace with actual eFiling hub URL
  }

  const createCase = () => {
    router.push('/get-started');
  };

  async function loadApplications() {
    dataLoaded.value = false;
    try {
      const applications = await chefsService.getSubmissions();
      previousApplications.value = (applications ?? []).map((appJson: any) => ({
        deceased_name: appJson.applicantName || '(the person who died)',
        app_type: 'Probate Grant',
        status: appJson.status ?? '',
        packageNum: appJson.chefsSubmissionId ?? '',
        id: appJson.id,
        chefsSubmissionId: appJson.chefsSubmissionId,
        lastUpdated: appJson.lastUpdatedAt
          ? moment(appJson.lastUpdatedAt)
              .tz('America/Vancouver')
              .diff('2000-01-01', 'minutes')
          : 0,
        lastUpdatedDate: appJson.lastUpdatedAt
          ? moment(appJson.lastUpdatedAt).tz('America/Vancouver').format()
          : '',
        lastFiled: appJson.lastFiledAt
          ? moment(appJson.lastFiledAt)
              .tz('America/Vancouver')
              .diff('2000-01-01', 'minutes')
          : 0,
        lastFiledDate: appJson.lastFiledAt
          ? moment(appJson.lastFiledAt).tz('America/Vancouver').format()
          : '',
      }));
    } catch (err: any) {
      if (err.response?.status === 404) {
        previousApplications.value = [];
      } else {
        error.value = err;
      }
    } finally {
      dataLoaded.value = true;
    }
  }

  // ── Resume Application ────────────────────────────────────────────────────────
  function resumeApplication(app: any) {
    router.push({
      name: 'ResumeApplication',
      params: { submissionId: app.chefsSubmissionId },
    });
  }

  // ── Delete Application ────────────────────────────────────────────────────────
  function removeApplication(application: any) {
    deleteError.value = false;
    deleteErrorMsg.value = '';
    deleteErrorMsgDesc.value = '';
    applicationToDelete.value = application;
    confirmDelete.value = true;
  }

  async function confirmRemoveApplication() {
    try {
      await chefsService.deleteSubmission(applicationToDelete.value.id);
      previousApplications.value = previousApplications.value.filter(
        (app) => app.id !== applicationToDelete.value.id
      );
      confirmDelete.value = false;
    } catch (err: any) {
      const errMsg =
        err.response?.data?.error ??
        err.response?.data?.message ??
        'Unknown error';
      deleteErrorMsg.value =
        errMsg.slice(0, 60) + (errMsg.length > 60 ? ' ...' : '');
      deleteErrorMsgDesc.value = errMsg;
      deleteError.value = true;
      confirmDelete.value = false;
    }
  }

  // ── Lifecycle ─────────────────────────────────────────────────────────────────
  onMounted(() => {
    loadApplications();
  });

  onUnmounted(() => {});
</script>
