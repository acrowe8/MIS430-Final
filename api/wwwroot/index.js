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
    // The next frontend step will consume this classification for course confirmation
    // or a handoff. Do not expose model-written text or invent a recommendation here.
    document.dispatchEvent(new CustomEvent('assignment-analyzed', { detail: result }));
    successBox.textContent = 'Your upload has been reviewed.';
    successBox.hidden = false;
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
