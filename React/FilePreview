import React from "react";
import PropTypes from "prop-types";

function FilePreview(props) {
  return (
    <li>
      <img
        src={props.file.preview}
        alt="No Preview Available"
        width={100}
        height={100}
        className="me-2 mt-3"
      />
      <p className="me-3">{props.file.name}</p>
    </li>
  );
}

FilePreview.propTypes = {
  file: PropTypes.shape({
    preview: PropTypes.string,
    name: PropTypes.string.isRequired,
  }),
};

export default FilePreview;
