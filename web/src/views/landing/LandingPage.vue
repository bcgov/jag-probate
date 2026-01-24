<template>
    <div class="position-relative text-dark">
        <section>
            <h1 class="text-center fw-bold">Represent Someone Who Died (also known as Probate)</h1>

            <div class="card shadow-sm border-0 info-card mb-4">
                <div class="card-body">
                    <div class="row justify-content-center mb-4">
                        <div class="col-12 text-center">
                            <p class="fs-5 text-muted mb-0">
                                This service will help you fill out the court forms required to apply for a
                                <ToolTip term="Representation Grant" />
                                from the Supreme Court of British Columbia when the deceased did not leave a will.
                            </p>
                        </div>
                    </div>
                    <div class="row g-4 align-items-start">
                        <div class="col-12 col-lg-6">
                            <h2 class="h4 fw-bold mb-3">Presently, this service can only be used if:</h2>
                            <ul class="list-unstyled mb-0 checklist">
                                <li>
                                    <font-awesome-icon icon="fas-solid fa-circle-check" class="text-success me-2" />The
                                    <strong>deceased did not leave a will</strong>.
                                </li>
                                <li><font-awesome-icon icon="fas-solid fa-circle-check"
                                        class="text-success me-2" /><strong>Only one person</strong> is applying for the
                                    Representation Grant.
                                </li>
                                <li class="d-block">
                                    <p><font-awesome-icon icon="fas-solid fa-circle-check"
                                            class="text-success me-2" />You
                                        are <strong>one of the following</strong>:
                                    </p>
                                    <ul class="disc-list ms-4">
                                        <li>The <strong>spouse</strong> of the deceased</li>
                                        <li>A <strong>child</strong> of the deceased</li>
                                        <li>A <strong>legal guardian, nominee, or personal representative</strong>
                                            acting for someone listed above</li>
                                    </ul>
                                </li>
                            </ul>
                        </div>

                        <div class="col-12 col-lg-6">
                            <h2 class="h4 fw-bold mb-3">This service CANNOT be used if:</h2>
                            <ul class="list-unstyled mb-0 checklist">
                                <li><font-awesome-icon icon="fas-solid fa-circle-xmark" class="text-danger me-2" />One
                                    of the
                                    deceased's <strong>children passed
                                        away before them</strong> and that child has children of their own.</li>
                                <li><font-awesome-icon icon="fas-solid fa-circle-xmark" class="text-danger me-2" />There
                                    is
                                    <strong>no surviving spouse, descendant,
                                        or creditor</strong> (other than yourself) who can be identified at this time.
                                </li>
                            </ul>
                        </div>
                    </div>
                </div>
            </div>

            <div class="card shadow-sm border-0 info-card info-card-blue text-white mb-4">
                <div class="card-body">
                    <div class="row align-items-start">
                        <div class="col-12 text-center">
                            <h2 class="fw-bold mb-2">How it Works?</h2>
                        </div>
                        <div class="col-12 col-lg-6">
                            <p class="fw-semibold mb-1">Step 1: Register or Login</p>
                            <p>
                                You will need a Basic BCeID account. If you do not currently have a Basic BCeID account
                                you can register for one.
                            </p>
                            <p class="fw-semibold mb-1">Step 2: Pre-qualifying Questions</p>
                            <p>
                                Once logged in, click Begin New Session to answer questions to ensure that this service
                                is appropriate for you to use.
                            </p>
                        </div>
                        <div class="col-12 col-lg-6">
                            <p class="fw-semibold mb-1">Step 3: Answer Questions</p>
                            <p>
                                Our secure online questionnaire uses your answers to fill out the court forms required.
                                You can save your work so you can proceed at your own pace, any time.
                            </p>
                            <p class="fw-semibold mb-1">Step 4: Print Your Forms</p>
                            <p>
                                Once you're done with the questionnaire, you will need to print your court forms and
                                file them at your local
                                <a class="link-light fw-semibold"
                                    href="https://www2.gov.bc.ca/gov/content/justice/courthouse-services/courthouse-locations"
                                    target="_blank" rel="noreferrer">Supreme Court registry</a>.
                            </p>
                        </div>
                    </div>
                </div>
            </div>

            <div class="row g-4 align-items-center text-center p-4">
                <div class="col-12 col-lg-6">
                    <h3 class="text-uppercase fw-semibold text-primary mb-1">New Users</h3>
                    <button type="button" class="btn btn-primary btn-lg px-4 mt-2" @click="handleRegister">Register for
                        a <strong>Basic</strong> BCeID</button>
                </div>
                <div class="col-12 col-lg-6">
                    <h3 class="text-uppercase fw-semibold text-primary mb-1">Returning Users</h3>
                    <button type="button" class="btn btn-warning btn-lg px-4 mt-2" @click="handleReturning">Use
                        an existing <strong>Basic</strong> BCeID</button>
                </div>
            </div>
        </section>

        <!-- Original Probate Application has a modal definition here see bcgov/representation-grant-app for details on implementation -->
    </div>
</template>

<script setup lang="ts">
import ToolTip from "@/components/shared/glossary/ToolTip.vue";
import { useLayoutStore } from "@/stores";
import { useRouter } from "vue-router";

const layoutStore = useLayoutStore();
layoutStore.setBackdropClass("bd-legislature-dome");

const router = useRouter();

const BCEID_REGISTER_URL = "https://www.bceid.ca/register/basic/account_details.aspx";

const handleRegister = () => {
    window.open(BCEID_REGISTER_URL, "_blank", "noopener");
};

const handleReturning = () => {
    router.push({ name: "Cases" });
};
</script>

<style scoped lang="scss">
.info-card {
    border-radius: 1rem;
    background-color: rgba(255, 255, 255, 0.8);
}

.info-card-blue {
    background-color: rgba(0, 51, 102, 0.8);
}

ul.disc-list li {
    list-style-type: disc;
}

.with-marker {
    position: relative;
    padding-left: 0.5rem;
}

.with-marker::marker {
    color: #003366;
}

.modal-backdrop {
    position: fixed;
    inset: 0;
    background-color: rgba(0, 0, 0, 0.4);
    z-index: 1050;
    padding: 1rem;
}

.modal-content {
    border-radius: 1rem;
    overflow: hidden;
}

.btn-outline-primary {
    border-width: 2px;
}

@media (max-width: 767.98px) {

    .info-card,
    .cta-card {
        border-radius: 0.75rem;
    }
}
</style>
