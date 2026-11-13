const fileInput = document.querySelector('#assignment-file');
const uploadButton = document.querySelector('#upload-button');
const studentMessage = document.querySelector('#student-message');
const progress = document.querySelector('#upload-progress');
const errorBox = document.querySelector('#upload-error');
const successBox = document.querySelector('#upload-success');
const allowedExtensions = new Set(['pdf', 'docx', 'png', 'jpg', 'jpeg', 'gif', 'webp']);
const maxFileBytes = 10 * 1024 * 1024;
let isUploading = false;

uploadButton.addEventListener('click', () => fileInput.click());
fileInput.addEventListener('change', async () => {
  const file = fileInput.files[0];
  if (!file || isUploading) return;
  errorBox.hidden = true;
  successBox.hidden = true;
  const extension = file.name.split('.').pop().toLowerCase();
  let validationError = '';
  if (!allowedExtensions.has(extension)) {
    validationError = 'That file type isn’t supported. Choose a PDF, Word (.docx), or PNG, JPG, GIF, or WEBP image.';
  } else if (file.size === 0) {
    validationError = 'That file is empty. Please choose an assignment with content.';
  } else if (file.size > maxFileBytes) {
    validationError = 'That file is too large. Please choose a file no larger than 10 MB.';
  }
  if (validationError) {
    showError(validationError);
    fileInput.value = '';
    return;
  }

  const formData = new FormData();
  formData.append('file', file);
  if (studentMessage.value.trim()) formData.append('message', studentMessage.value.trim());
  setLoading(true);
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 30000);
  try {
    // Keep uploads in this request only; never persist them in browser storage.
    const response = await fetch('/api/analyze', {
      method: 'POST', body: formData, signal: controller.signal
    });
    if (!response.ok) {
      if (response.status === 413) throw new Error('That file is too large for the server. Please choose a smaller file.');
      if (response.status === 400) throw new Error('We couldn’t read that file. Please try another PDF, Word document, or image.');
      throw new Error('We couldn’t review your upload right now. Please try again.');
    }
    const result = await response.json();
    if (!result || !['academic', 'non_academic', 'distress'].includes(result.request_category)) {
      throw new Error('We couldn’t read the upload response. Please try again.');
    }
    showAnalysis(result);
    studentMessage.value = '';
  } catch (error) {
    showError(error.name === 'AbortError'
      ? 'This is taking longer than expected. Please try uploading again.'
      : error instanceof TypeError
        ? 'We couldn’t connect. Check your connection and try again.'
        : error instanceof SyntaxError
          ? 'We couldn’t read the upload response. Please try again.'
          : error.message);
  } finally {
    clearTimeout(timeout);
    fileInput.value = '';
    setLoading(false);
  }
});

function showError(message) {
  errorBox.textContent = message;
  errorBox.hidden = false;
}

function setLoading(loading) {
  isUploading = loading;
  uploadButton.disabled = loading;
  studentMessage.disabled = loading;
  progress.hidden = !loading;
  uploadButton.setAttribute('aria-busy', String(loading));
}

const panels = ['upload', 'confirm', 'search', 'assignment', 'selected', 'recommendation'];
// These seven values match the classifier, seed data, and recommendation API contract.
const assignmentTypes = new Map([
  ['essay', 'Essay or paper'],
  ['lab_report', 'Lab report'],
  ['problem_set', 'Problem set or homework'],
  ['coding_project', 'Coding project'],
  ['presentation', 'Presentation'],
  ['exam_prep', 'Exam prep'],
  ['other', 'Other']
]);
const courseSearch = document.querySelector('#course-search');
const courseResults = document.querySelector('#course-results');
const searchStatus = document.querySelector('#search-status');
const retrySearch = document.querySelector('#retry-search');
const startOver = document.querySelector('#start-over');
let classification = null;
let selectedCourse = null;
let selectedAssignmentType = null;
let searchTimer;
let searchController;
let searchVersion = 0;

function showPanel(name) {
  cancelSearch();
  if (name !== 'recommendation') cancelRecommendation();
  for (const panel of panels) {
    document.querySelector(`#${panel}-panel`).hidden = panel !== name;
  }
  startOver.hidden = name === 'upload';
}

function validCourse(course) {
  return course && Number.isInteger(course.course_id) && course.course_id > 0
    && typeof course.code === 'string' && course.code.trim()
    && typeof course.title === 'string' && course.title.trim();
}

function courseLabel(course) {
  return `${course.code} – ${course.title}`;
}

function showAnalysis(result) {
  classification = result;
  selectedCourse = null;
  selectedAssignmentType = null;
  // Non-academic and distress responses go directly to the backend handoff.
  if (result.request_category !== 'academic') {
    void loadRecommendation({
      course_id: null,
      assignment_type: 'other',
      request_category: result.request_category,
      asked_for_answers: result.asked_for_answers === true
    });
    return;
  }
  if (['high', 'low'].includes(result.course_confidence) && validCourse(result)) {
    showPanel('confirm');
    document.querySelector('#course-guess').textContent = courseLabel(result);
    document.querySelector('#confirm-heading').textContent = `Is this for ${result.code}?`;
    document.querySelector('#confirm-heading').focus();
  } else {
    openSearch('We couldn’t identify your course. Search for it below.');
  }
}

function openSearch(intro) {
  showPanel('search');
  document.querySelector('#search-intro').textContent = intro;
  courseSearch.value = '';
  courseResults.replaceChildren();
  retrySearch.hidden = true;
  searchStatus.textContent = 'Enter a course code or title to start searching.';
  courseSearch.focus();
}

function selectCourse(course) {
  if (classification?.request_category !== 'academic' || !validCourse(course)) return;
  selectedCourse = { course_id: course.course_id, code: course.code, title: course.title };
  document.dispatchEvent(new CustomEvent('course-confirmed', {
    detail: { ...classification, ...selectedCourse }
  }));
  // Retain an answered question when the course changes. A fresh upload resets it.
  if (!selectedAssignmentType && classification.type_confidence === 'high'
      && assignmentTypes.has(classification.assignment_type)) {
    selectedAssignmentType = classification.assignment_type;
  }
  if (!selectedAssignmentType) {
    showPanel('assignment');
    document.querySelector('#assignment-course').textContent = courseLabel(selectedCourse);
    document.querySelector('#assignment-heading').focus();
    return;
  }
  showSelectedDetails();
}

for (const [value, label] of assignmentTypes) {
  const button = document.createElement('button');
  button.type = 'button';
  button.className = 'btn btn-outline-secondary text-start';
  button.textContent = label;
  button.addEventListener('click', () => {
    if (!selectedCourse || classification?.request_category !== 'academic'
        || document.querySelector('#assignment-panel').hidden) return;
    selectedAssignmentType = value;
    showSelectedDetails();
  });
  document.querySelector('#assignment-options').append(button);
}

function showSelectedDetails() {
  showPanel('selected');
  document.querySelector('#selected-course').textContent = courseLabel(selectedCourse);
  document.querySelector('#selected-assignment').textContent = assignmentTypes.get(selectedAssignmentType);
  document.querySelector('#selected-heading').focus();
  // Start the recommendation screen without an additional click or AI call.
  // Pass "other" unchanged; resource choice and fallback belong to the backend.
  document.dispatchEvent(new CustomEvent('recommendation-ready', {
    detail: {
      course_id: selectedCourse.course_id,
      assignment_type: selectedAssignmentType,
      request_category: classification.request_category,
      asked_for_answers: classification.asked_for_answers === true
    }
  }));
}

document.querySelector('#confirm-course').addEventListener('click', () => selectCourse(classification));
document.querySelector('#reject-course').addEventListener('click', () => openSearch('Let’s find the right class. Search for your course below.'));
document.querySelector('#change-course').addEventListener('click', () => openSearch('Choose a different course for this assignment.'));
startOver.addEventListener('click', () => {
  showPanel('upload');
  classification = null;
  selectedCourse = null;
  selectedAssignmentType = null;
  courseResults.replaceChildren();
  courseSearch.value = '';
  fileInput.value = '';
  studentMessage.value = '';
  errorBox.hidden = true;
  successBox.hidden = true;
  uploadButton.focus();
});

function cancelSearch() {
  clearTimeout(searchTimer);
  searchController?.abort();
  searchVersion++;
}

courseSearch.addEventListener('input', () => {
  // Invalidate immediately, even during the debounce, so old results cannot be selected.
  cancelSearch();
  courseResults.replaceChildren();
  retrySearch.hidden = true;
  if (!courseSearch.value.trim()) {
    searchStatus.textContent = 'Enter a course code or title to start searching.';
    return;
  }
  searchStatus.textContent = 'Searching courses…';
  searchTimer = setTimeout(searchCourses, 300);
});
retrySearch.addEventListener('click', searchCourses);

async function searchCourses() {
  cancelSearch();
  const term = courseSearch.value.trim();
  if (!term || document.querySelector('#search-panel').hidden) return;
  const version = searchVersion;
  const controller = new AbortController();
  searchController = controller;
  const timeout = setTimeout(() => controller.abort(), 10000);
  courseResults.replaceChildren();
  retrySearch.hidden = true;
  searchStatus.textContent = 'Searching courses…';
  try {
    const response = await fetch(`/api/courses?search=${encodeURIComponent(term)}`, { signal: controller.signal });
    if (!response.ok) throw new Error('Search failed');
    const courses = await response.json();
    if (!Array.isArray(courses) || !courses.every(validCourse)) throw new Error('Invalid courses');
    if (version !== searchVersion) return;
    searchStatus.textContent = courses.length
      ? `${courses.length} matching ${courses.length === 1 ? 'course' : 'courses'}. Select your course below.`
      : 'No matching courses. Try a different course code or title.';
    for (const course of courses) {
      const item = document.createElement('li');
      item.className = 'list-group-item p-0';
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'course-choice';
      // Database text is displayed as text, never interpreted as HTML.
      button.textContent = courseLabel(course);
      button.addEventListener('click', () => selectCourse(course));
      item.append(button);
      courseResults.append(item);
    }
  } catch (error) {
    if (version !== searchVersion) return;
    searchStatus.textContent = error.name === 'AbortError'
      ? 'The search took too long. Please try again.'
      : 'We couldn’t load courses. Check your connection and try again.';
    retrySearch.hidden = false;
  } finally {
    clearTimeout(timeout);
  }
}

const missingInfo = 'Contact the office to confirm.';
let recommendationController;
let recommendationVersion = 0;
let recommendationRequest = null;

function cancelRecommendation() {
  recommendationController?.abort();
  recommendationVersion++;
  recommendationRequest = null;
}

document.addEventListener('recommendation-ready', event => {
  void loadRecommendation(event.detail);
});
document.querySelector('#retry-recommendation').addEventListener('click', () => {
  if (recommendationRequest) void loadRecommendation(recommendationRequest);
});
document.querySelector('#recommendation-change-course').addEventListener('click', () => {
  openSearch('Choose a different course for this assignment.');
});

async function loadRecommendation(payload) {
  cancelRecommendation();
  recommendationRequest = { ...payload };
  const version = recommendationVersion;
  const controller = new AbortController();
  recommendationController = controller;
  showPanel('recommendation');
  const heading = document.querySelector('#recommendation-heading');
  heading.textContent = 'Finding your support…';
  heading.focus();
  document.querySelector('#recommendation-context').textContent = selectedCourse
    ? `${courseLabel(selectedCourse)} · ${assignmentTypes.get(selectedAssignmentType)}` : '';
  for (const id of ['recommendation-error', 'retry-recommendation', 'recommendation-resource', 'recommendation-handoff', 'recommendation-refusal']) {
    document.querySelector(`#${id}`).hidden = true;
  }
  document.querySelector('#recommendation-loading').hidden = false;
  document.querySelector('#recommendation-panel').setAttribute('aria-busy', 'true');
  document.querySelector('#recommendation-change-course').hidden = !selectedCourse;
  const timeout = setTimeout(() => controller.abort(), 10000);
  try {
    const response = await fetch('/api/recommend', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(recommendationRequest),
      signal: controller.signal
    });
    if (!response.ok) throw new Error('Recommendation unavailable');
    const result = await response.json();
    if (!result || !['recommendation', 'non_academic', 'distress'].includes(result.type)) {
      throw new Error('Invalid recommendation');
    }
    if (version !== recommendationVersion) return;
    renderRecommendation(result);
    heading.focus();
  } catch (error) {
    if (version !== recommendationVersion) return;
    heading.textContent = 'Let’s try that again.';
    const errorBox = document.querySelector('#recommendation-error');
    errorBox.textContent = error.name === 'AbortError'
      ? 'Finding support took too long. Please try again.'
      : 'We couldn’t load your recommendation. Please check your connection and try again.';
    errorBox.hidden = false;
    document.querySelector('#retry-recommendation').hidden = false;
    heading.focus();
  } finally {
    clearTimeout(timeout);
    if (version === recommendationVersion) {
      document.querySelector('#recommendation-loading').hidden = true;
      document.querySelector('#recommendation-panel').setAttribute('aria-busy', 'false');
    }
  }
}

function displayText(value) {
  return typeof value === 'string' && value.trim() ? value.trim() : missingInfo;
}

function safeWebLink(value) {
  if (typeof value !== 'string' || !value.trim()) return null;
  try {
    const url = new URL(value);
    return ['https:', 'http:'].includes(url.protocol) && !url.username && !url.password ? url.href : null;
  } catch { return null; }
}

function addLink(container, text, href) {
  const link = document.createElement('a');
  link.textContent = text;
  link.href = href;
  link.target = '_blank';
  link.rel = 'noopener noreferrer';
  container.append(link);
}

function renderRecommendation(result) {
  const heading = document.querySelector('#recommendation-heading');
  document.querySelector('#recommendation-refusal').hidden = !(result.declined_assignment_help === true
    || recommendationRequest?.asked_for_answers === true);
  if (result.type !== 'recommendation') {
    heading.textContent = result.type === 'distress' ? 'Support is available.' : 'Talk with someone at UA.';
    const message = document.querySelector('#recommendation-handoff');
    // Keep internal data-verification placeholders out of student-facing messages.
    message.textContent = displayText(result.message).replace(/TODO[^.]*\./gi, missingInfo);
    message.hidden = false;
    document.querySelector('#recommendation-change-course').hidden = true;
    document.querySelector('#recommendation-refusal').hidden = true;
    return;
  }
  heading.textContent = displayText(result.resource_name);
  document.querySelector('#resource-reason').textContent = displayText(result.reason);
  const location = document.querySelector('#resource-location');
  location.replaceChildren();
  if (result.is_online === true) {
    const onlineLink = safeWebLink(result.online_link);
    if (onlineLink) addLink(location, 'Online — open session link', onlineLink);
    else location.textContent = `Online. ${missingInfo}`;
  } else if (result.is_online === false) {
    if (typeof result.building === 'string' && result.building.trim()) {
      addLink(location, result.building.trim(), 'https://www.ua.edu/map/');
      const room = document.createElement('span');
      room.textContent = ` · Room: ${displayText(result.room)}`;
      location.append(room);
    } else location.textContent = missingInfo;
  } else location.textContent = missingInfo;
  document.querySelector('#resource-hours').textContent = displayText(result.next_open);
  document.querySelector('#resource-session').textContent = {
    drop_in: 'Drop in during open hours', appointment: 'Appointment required'
  }[result.session_type] || missingInfo;
  document.querySelector('#resource-email').textContent = displayText(result.contact_email);
  document.querySelector('#resource-phone').textContent = displayText(result.contact_phone);
  const booking = document.querySelector('#resource-booking');
  const bookingLink = safeWebLink(result.booking_url);
  booking.hidden = !bookingLink;
  booking.removeAttribute('href');
  if (bookingLink) booking.href = bookingLink;
  const bookingMissing = document.querySelector('#resource-booking-missing');
  bookingMissing.hidden = !!bookingLink || result.session_type !== 'appointment';
  bookingMissing.textContent = `Booking: ${missingInfo}`;
  document.querySelector('#resource-verified').textContent = `Info last checked: ${displayText(result.last_verified)}`;
  document.querySelector('#resource-stale').hidden = result.is_stale !== true;
  document.querySelector('#resource-syllabus').hidden = result.show_syllabus_tip !== true;
  document.querySelector('#recommendation-resource').hidden = false;
}
