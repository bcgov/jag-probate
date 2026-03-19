<template>
    <div id="status" class="card bg-white border-white" :style="{ minHeight: getHeight }">
        <div class="card home-content border-white p-0">

            <!-- Error Banner -->
            <div class="alert alert-danger mt-4" v-if="error">{{ error }}</div>

            <!-- Header Section -->
            <div name="info-content-header" class="pt-5" ref="infoContentHeader">
                <h1>Previous Activity</h1>
                <div class="card bg-white border-white">
                    <p class="ml-0 mb-1">
                        To resume a previous session, click the Resume button next to the activity.
                        To start a new session, click the Begin New Session button at the bottom of the page.
                    </p>
                </div>
                <div class="mb-4 border border-gray border-right-0 border-left-0 border-bottom-0"></div>
            </div>

            <!-- Loading Spinner -->
            <loading-spinner v-if="!dataLoaded" waitingText="Loading ..." />

            <!-- Table Section -->
            <div v-if="dataLoaded" :style="{ height: getTableHeight }">

                <!-- Empty State -->
                <div class="card bg-white border-white" v-if="!previousApplications.length">
                    <span class="text-muted ml-4 mb-5">No previous applications.</span>
                </div>

                <!-- Applications Table -->
                <div v-else class="card bg-white border-white" :style="{ height: getTableHeight }">
                    <div class="mx-0 b-table-sticky-header table-responsive-sm"
                        style="max-height: 600px; overflow-y: auto;">
                        <table role="table" class="table b-table table-striped table-borderless table-sm">
                            <thead role="rowgroup" class="thead-dark">
                                <tr role="row">
                                    <th style="font-size: 11pt; width: 20%">Application</th>
                                    <th style="font-size: 11pt; width: 20%">Last Updated</th>
                                    <th style="font-size: 11pt; width: 20%">Last Filed</th>
                                    <th style="font-size: 10pt; width: 15%">Status</th>
                                    <th style="font-size: 10pt; width: 15%">Package#</th>
                                    <th style="font-size: 10pt; width: 10%"></th>
                                </tr>
                            </thead>
                            <tbody role="rowgroup">
                                <tr v-for="app in previousApplications" :key="app.id" role="row">
                                    <td class="border-top">{{ formatFullName(app.deceased_name) }}</td>
                                    <td class="border-top">{{ beautifyDate(app.lastUpdatedDate) }}</td>
                                    <td class="border-top">{{ beautifyDate(app.lastFiledDate) }}</td>
                                    <td class="border-top">{{ app.status }}</td>
                                    <td class="border-top">{{ app.packageNum }}</td>
                                    <td class="border-top">
                                        <!-- Trash: only shown when application has never been filed -->
                                        <button v-if="app.lastFiled === 0" title="Remove Application" type="button"
                                            class="btn my-0 py-0 border-0 btn-transparent btn-sm"
                                            @click="removeApplication(app)">
                                            <svg viewBox="0 0 16 16" width="1.25em" height="1.25em" fill="currentColor"
                                                class="text-danger" xmlns="http://www.w3.org/2000/svg">
                                                <path fill-rule="evenodd"
                                                    d="M2.5 1a1 1 0 0 0-1 1v1a1 1 0 0 0 1 1H3v9a2 2 0 0 0 2 2h6a2 2 0 0 0 2-2V4h.5a1 1 0 0 0 1-1V2a1 1 0 0 0-1-1H10a1 1 0 0 0-1-1H7a1 1 0 0 0-1 1H2.5zm3 4a.5.5 0 0 1 .5.5v7a.5.5 0 0 1-1 0v-7a.5.5 0 0 1 .5-.5zM8 5a.5.5 0 0 1 .5.5v7a.5.5 0 0 1-1 0v-7A.5.5 0 0 1 8 5zm3 .5a.5.5 0 0 0-1 0v7a.5.5 0 0 0 1 0v-7z" />
                                            </svg>
                                        </button>

                                        <!-- Resume (pencil) -->
                                        <button title="Resume Application" type="button"
                                            class="btn my-0 py-0 border-0 btn-transparent btn-sm"
                                            @click="resumeApplication(app.id)">
                                            <svg viewBox="0 0 16 16" width="1.25em" height="1.25em" fill="currentColor"
                                                class="text-primary" xmlns="http://www.w3.org/2000/svg">
                                                <path
                                                    d="M15.502 1.94a.5.5 0 0 1 0 .706L14.459 3.69l-2-2L13.502.646a.5.5 0 0 1 .707 0l1.293 1.293zm-1.75 2.456l-2-2L4.939 9.21a.5.5 0 0 0-.121.196l-.805 2.414a.25.25 0 0 0 .316.316l2.414-.805a.5.5 0 0 0 .196-.12l6.813-6.814z" />
                                                <path fill-rule="evenodd"
                                                    d="M1 13.5A1.5 1.5 0 0 0 2.5 15h11a1.5 1.5 0 0 0 1.5-1.5v-6a.5.5 0 0 0-1 0v6a.5.5 0 0 1-.5.5h-11a.5.5 0 0 1-.5-.5v-11a.5.5 0 0 1 .5-.5H9a.5.5 0 0 0 0-1H2.5A1.5 1.5 0 0 0 1 2.5v11z" />
                                            </svg>
                                        </button>

                                        <!-- Paper plane: only shown when application has been filed -->
                                        <button v-if="app.lastFiled !== 0" title="Navigate To Submitted Application"
                                            type="button" class="btn my-0 py-0 border-0 btn-transparent btn-sm"
                                            @click="navigateToEFilingHub(app.id)">
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

        <!-- Button Menu -->
        <div name="button-menu" class="card button-content border-white bg-white" ref="buttonMenu">
            <div class="card-body">
                <div class="row mt-2 ml-5">
                    <div class="col-6"></div>
                    <div class="col-3 m-0 p-0">
                        <button type="button" class="btn btn-success application-button" @click="preQualify">
                            Begin NEW Session
                        </button>
                    </div>
                    <div class="col-3 m-0 p-0">
                        <div class="my-2 ml-5">
                            <a class="terms" @click="openTerms">
                                <u style="cursor: pointer">Terms and Conditions</u>
                            </a>
                        </div>
                    </div>
                </div>
            </div>
        </div>

        <!-- Delete Confirmation Modal -->
        <div v-if="confirmDelete" class="modal d-block" tabindex="-1" role="dialog">
            <div class="modal-dialog" role="document">
                <div class="modal-content">
                    <div class="modal-header bg-warning text-light">
                        <h2 class="modal-title mb-0 text-light">Confirm Delete Application</h2>
                        <button type="button" class="btn btn-outline-warning text-light" @click="confirmDelete = false">
                            &times;
                        </button>
                    </div>
                    <div class="modal-body">
                        <div v-if="deleteError" class="mb-3">
                            <span class="badge bg-danger text-white p-2 delete-error-badge" :title="deleteErrorMsgDesc">
                                {{ deleteErrorMsg }}
                                <button type="button" class="btn-close btn-close-white ms-3"
                                    @click="deleteError = false" />
                            </span>
                        </div>
                        <h4>
                            Are you sure you want to delete your
                            <b>"{{ applicationToDelete?.app_type }}"</b> application?
                        </h4>
                    </div>
                    <div class="modal-footer">
                        <button type="button" class="btn btn-danger" @click="confirmRemoveApplication">Confirm</button>
                        <button type="button" class="btn btn-primary" @click="confirmDelete = false">Cancel</button>
                    </div>
                </div>
            </div>
        </div>
        <div v-if="confirmDelete" class="modal-backdrop fade show"></div>

    </div>
</template>

<script setup lang="ts">

import { useLocationStore } from '@/stores/LocationStore';
import { useApplicationStore } from '@/stores/PreviousApplicationStore';
import axios from 'axios';
import moment from 'moment-timezone';
import { computed, onMounted, onUnmounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import './ApplicationStatus.styles.css';

// ── Router & Store ────────────────────────────────────────────────────────────
const router = useRouter();

const applicationStore = useApplicationStore();
const locationStore = useLocationStore();

// ── State ─────────────────────────────────────────────────────────────────────
const previousApplications = ref<any[]>([]);
const dataLoaded = ref(false);
const error = ref('');

const confirmDelete = ref(false);
const applicationToDelete = ref<any>({});
const deleteError = ref(false);
const deleteErrorMsg = ref('');
const deleteErrorMsgDesc = ref('');

// ── Layout / Height Tracking ──────────────────────────────────────────────────
const windowHeight = ref(0);
const footerHeight = ref(0);
const headerHeight = ref(0);
const buttonMenuHeight = ref(0);
const infoContentHeaderHeight = ref(0);

const buttonMenu = ref<HTMLElement | null>(null);
const infoContentHeader = ref<HTMLElement | null>(null);

const getHeight = computed(
    () => `${windowHeight.value - footerHeight.value - headerHeight.value - 1}px`
);

const getTableHeight = computed(
    () =>
        `${windowHeight.value -
        footerHeight.value -
        headerHeight.value -
        buttonMenuHeight.value -
        infoContentHeaderHeight.value -
        10
        }px`
);

function getWindowHeight() {
    windowHeight.value = document.documentElement.clientHeight;
    footerHeight.value =
        (document.querySelector('[name="navigation-footer"]') as HTMLElement)?.clientHeight ?? 0;
    headerHeight.value =
        (document.querySelector('[name="navigation-topbar"]') as HTMLElement)?.clientHeight ?? 0;
    buttonMenuHeight.value = buttonMenu.value?.clientHeight ?? 0;
    infoContentHeaderHeight.value = infoContentHeader.value?.clientHeight ?? 0;
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
    return moment(dateStr).tz('America/Vancouver').format('ddd, MMM DD YYYY HH:mm');
}

// ── Navigation ────────────────────────────────────────────────────────────────
function preQualify() {
    router.push({ name: 'pre-qualification' });
}

function openTerms() {
    router.push({ name: 'terms' });
}

function navigateToEFilingHub(id: number) {
    console.log('going to hub', id);
    // TODO: replace with actual eFiling hub URL
}

// ── Data Fetching ─────────────────────────────────────────────────────────────
async function extractFilingLocations() {
    try {
        const response = await axios.get('/efiling/locations/');
        const locationsInfo = response.data;
        const locations = Object.keys(locationsInfo).map((location) => {
            const info = locationsInfo[location];
            const address = [info.address_1, info.address_2, info.address_3]
                .filter(Boolean)
                .join(', ');
            return {
                id: info.location_id,
                name: location,
                address,
                city: info.city ?? '',
                postalCode: info.postal ?? '',
                province: info.province ?? '',
            };
        });
        locationStore.setLocationsInfo(locations);
    } catch (err) {
        console.error(err);
    } finally {
        await loadApplications();
    }
}

async function loadApplications() {
    dataLoaded.value = false;
    try {
        const response = await axios.get('/app-list/');
        previousApplications.value = response.data.map((appJson: any) => ({
            deceased_name: appJson.deceased_name,
            lastUpdated: appJson.last_updated
                ? moment(appJson.last_updated).tz('America/Vancouver').diff('2000-01-01', 'minutes')
                : 0,
            lastUpdatedDate: appJson.last_updated
                ? moment(appJson.last_updated).tz('America/Vancouver').format()
                : '',
            lastFiled: appJson.last_filed
                ? moment(appJson.last_filed).tz('America/Vancouver').diff('2000-01-01', 'minutes')
                : 0,
            lastFiledDate: appJson.last_filed
                ? moment(appJson.last_filed).tz('America/Vancouver').format()
                : '',
            id: appJson.id,
            app_type: appJson.app_type,
            status: appJson.status ?? '',
            packageNum: appJson.package_num ?? '',
        }));
    } catch (err: any) {
        error.value = err;
    } finally {
        dataLoaded.value = true;
    }
}

// ── Resume Application ────────────────────────────────────────────────────────
async function resumeApplication(applicationId: number) {
    try {
        const response = await axios.get(`/app/${applicationId}/`);
        applicationStore.setCurrentApplication(response.data);
        applicationStore.setExistingApplication(true);
        applicationStore.updateStPgNo();
        router.push({ name: 'surveys' });
    } catch (err: any) {
        error.value = err;
    }
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
        await axios.delete(`/app/${applicationToDelete.value.id}/`);
        previousApplications.value = previousApplications.value.filter(
            (app) => app.id !== applicationToDelete.value.id
        );
        confirmDelete.value = false;
    } catch (err: any) {
        const errMsg = err.response?.data?.error ?? 'Unknown error';
        deleteErrorMsg.value = errMsg.slice(0, 60) + (errMsg.length > 60 ? ' ...' : '');
        deleteErrorMsgDesc.value = errMsg;
        deleteError.value = true;
        confirmDelete.value = false;
    }
}

// ── Lifecycle ─────────────────────────────────────────────────────────────────
onMounted(() => {
    window.addEventListener('resize', getWindowHeight);
    getWindowHeight();
    extractFilingLocations();
});

onUnmounted(() => {
    window.removeEventListener('resize', getWindowHeight);
});
</script>